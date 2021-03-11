using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
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

		public Task<IP4Addr> ResolveRecursive(string domain)
		{
			return Task<IP4Addr>.Run(() => {
				string[] domains = domain.Split('.');
				Array.Reverse(domains);
				IP4Addr res = dnsClient.GetRootServers()[0];
				String subdomain = null;

				for (var i = 0; i < domains.Length; i++) {
					// The subdomain name we're querying the server with
					subdomain = domains[i];
					// Full path of the subdomain we are resolving. For mff.cuni.cz, it would be:
					// cz -> cuni.cz -> mff.cuni.cz
					var fullSubdomain = String.Join(".", domains.Take(i + 1).Reverse().ToList());
				
					// Directly resolve the domain (slow)
					var fallbackQuery = this.dnsClient.Resolve(res, subdomain);

					bool isCacheUsed = false;
					// In the meantime, check if the subdomain's IP is cached
					if (this.cache.ContainsKey(fullSubdomain)) {
						IP4Addr cachedIP;
						if (this.cache.TryGetValue(fullSubdomain, out cachedIP)) {
							// Possible cache hit, check if the record is still valid
							var cacheQuery = this.dnsClient.Reverse(cachedIP);
							// Guaranteed (?) to be faster than the fallbackQuery
							// TODO: Wait for both Tasks, just in case the fallbackQuery finishes first
							cacheQuery.Wait();
							if (cacheQuery.Result == fullSubdomain) {
								Console.WriteLine($"{domain} -- Cache hit for {fullSubdomain} as {res}");
								res = cachedIP;
								isCacheUsed = true;
							} else {
								this.cache.TryRemove(fullSubdomain, out _);
							}
						}
					}
					// Cache failed, resolve directly
					if (!isCacheUsed) {
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
