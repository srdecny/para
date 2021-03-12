using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace dns_netcore
{
	
	class RecursiveResolver : IRecursiveResolver
	{
		private IDNSClient dnsClient;
		private ConcurrentDictionary<string, (IP4Addr, DateTime)> cache;
		private uint queryCounter;
		private static readonly int TTL = 1000; //ms
		public RecursiveResolver(IDNSClient client)
		{
			this.dnsClient = client;
			this.cache = new ConcurrentDictionary<string, (IP4Addr, DateTime)>();
			queryCounter = 0;
		}
		// Given a domain (mff.cuni.cz), creates a list of all subdomains, e.g:
		// [cz, cuni.cz, mff.cuni.cz]
		private List<string> generateSubdomains(string domain) {
			var domains = domain.Split(".").Reverse().ToList();
			var subdomains = new List<string>();
			for (var i = 0; i < domains.Count; i++) {
				subdomains.Add(String.Join(".", domains.Take(i + 1).Reverse().ToList()));
			}
			return subdomains;
		}

		public Task<IP4Addr> ResolveRecursive(string domain)
		{
			return Task<IP4Addr>.Run(() => {
				string[] domains = domain.Split('.');
				var subdomains = generateSubdomains(domain);
				Array.Reverse(domains);

				// Distribute the queries to the root servers in a Round Robin way
				// queryCounter is uint so it won't overflow to negative numbers
				uint rootServerIndex = System.Threading.Interlocked.Increment(ref this.queryCounter);
				IP4Addr res = dnsClient.GetRootServers()[(int)rootServerIndex % dnsClient.GetRootServers().Count];
				String subdomain = null;

				for (var i = 0; i < domains.Length; i++) {
					// The subdomain name we're querying the server with
					subdomain = domains[i];
					// Full path of the subdomain we are resolving. For mff.cuni.cz, it would be (in order of iteration):
					// cz -> cuni.cz -> mff.cuni.cz
					var fullSubdomain = String.Join(".", domains.Take(i + 1).Reverse().ToList());
				
					// Directly resolve the domain (slow)
					var fallbackQuery = this.dnsClient.Resolve(res, subdomain);

					// In the meantime, check if any subdomain IP is cached
					(IP4Addr address, DateTime time) cacheRecord;
					var cacheValidations = new List<((string name, IP4Addr address) cacheRecord, Task<string> query)>();
					// Try all subdomains of the current domain, starting from the longest subdomain
					foreach (var cachedSubdomain in subdomains.Skip(i).Reverse()) {
						if (this.cache.TryGetValue(cachedSubdomain, out cacheRecord)) {
							// Possible cache hit, check if the record is still valid
							TimeSpan cacheRecordAge = cacheRecord.time - DateTime.Now;
							// Record is young enough, trust it 
							if ((int)cacheRecordAge.TotalMilliseconds < RecursiveResolver.TTL) {
								res = cacheRecord.address;
								i = cachedSubdomain.Split(".").Length - 1;	
								// Console.WriteLine($"{domain} -- Unvalidated cache hit {cachedSubdomain} to {res}");
								goto End; // oof
							// Record is too old, validate it with reverse query
							} else {
								var cacheQuery = this.dnsClient.Reverse(cacheRecord.address);
								cacheValidations.Add(((cachedSubdomain, cacheRecord.address), cacheQuery));
							}
						}
					}
					// Wait for the first Reverse Task to finish and then wait a bit for the other Tasks
					// That way, a stuck Task will not block
					Task.WaitAny(cacheValidations.Select(i => i.query).ToArray(), 110);
					System.Threading.Thread.Sleep(10);

					var finishedTasks = cacheValidations.FindAll(r => r.query.Status == TaskStatus.RanToCompletion).ToList();

					foreach (var validation in finishedTasks) {
						if (validation.cacheRecord.name != validation.query.Result) {
							this.cache.TryRemove(validation.cacheRecord.name, out _);
						} else {
							// Validated, refresh the TTL
							this.cache.TryAdd(validation.cacheRecord.name, (validation.cacheRecord.address, DateTime.Now));
						}
					}
					// Find "longest" subdomain that is validated
					var validatedSubdomain = finishedTasks.FindAll(validation => validation.cacheRecord.name == validation.query.Result)
							.Select(validation => validation.cacheRecord.name)
							.OrderByDescending(x => x.Split(".").Length)
							.FirstOrDefault();

					// Using cached address
					if (!string.IsNullOrEmpty(validatedSubdomain) && this.cache.TryGetValue(validatedSubdomain, out cacheRecord)) {
						// Calculate how many subdomains we've jumped ahead by using the cached results
						// Console.WriteLine($"Query {domain} -- Validated cache hit {validatedSubdomain} to {res}");
						i = validatedSubdomain.Split(".").Length - 1;
						res = cacheRecord.address;
					} else {
						fallbackQuery.Wait();
						res = fallbackQuery.Result;
						this.cache.TryAdd(fullSubdomain, (res, DateTime.Now));
					}
					End: ;

				}
				this.cache.TryAdd(domain, (res, DateTime.Now));
				return res;
			});
		}
	}
}
