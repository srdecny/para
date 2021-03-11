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
		private ConcurrentDictionary<string, IP4Addr> cache;

		public RecursiveResolver(IDNSClient client)
		{
			this.dnsClient = client;
			this.cache = new ConcurrentDictionary<string, IP4Addr>();
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
				IP4Addr res = dnsClient.GetRootServers()[0];
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
					IP4Addr cachedIP;
					var cacheValidations = new List<(string, Task<string>)>();
					// Try all subdomains of the current domain
					foreach (var cachedSubdomain in subdomains.Skip(i)) {
						if (this.cache.TryGetValue(fullSubdomain, out cachedIP)) {
							// Possible cache hit, check if the record is still valid
							var cacheQuery = this.dnsClient.Reverse(cachedIP);
							cacheValidations.Add((cachedSubdomain, cacheQuery));
						}
					}
					// Wait for the first Reverse Task to finish and then wait a bit for the other Tasks
					// That way, a stuck Task will not block
					Task.WaitAny(cacheValidations.Select(i => i.Item2).ToArray(), 550);
					System.Threading.Thread.Sleep(50);

					var finishedTasks = cacheValidations.FindAll(r => r.Item2.Status == TaskStatus.RanToCompletion).ToList();
					// Remove failed Reverse checks from the cache
					foreach (var failedValidation in finishedTasks.FindAll(validation => validation.Item1 != validation.Item2.Result)) {
						this.cache.TryRemove(failedValidation.Item1, out _);
					}
					// Find "longest" subdomain that is validated
					var validatedSubdomain = finishedTasks.FindAll(validation => validation.Item1 == validation.Item2.Result)
							.Select(validation => validation.Item1)
							.OrderBy(x => x.Split(".").Length)
							.FirstOrDefault();

					// Using cached address
					if (!string.IsNullOrEmpty(validatedSubdomain) && this.cache.TryGetValue(validatedSubdomain, out res)) {
						// Calculate how many subdomains we've jumped ahead by using the cached results
						i = validatedSubdomain.Split(".").Length - 1;
					} else {
						fallbackQuery.Wait();
						res = fallbackQuery.Result;
						// Console.WriteLine($"Resolved {subdomain} to {res}");
						this.cache.TryAdd(fullSubdomain, res);
						// Console.WriteLine($"Caching {fullSubdomain}");
					}

				}
				this.cache.TryAdd(domain, res);
				// Console.WriteLine($"Caching {domain} to {res}");
				return res;
			});
		}
	}
}
