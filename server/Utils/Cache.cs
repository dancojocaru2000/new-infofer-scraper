using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Utils;

public class Cache<TKey, TValue> where TKey: notnull {
	private readonly IDictionary<TKey, (TValue Data, DateTimeOffset FetchTime)> cache;

	public Func<TKey, TValue> Fetcher { get; init; }
	public TimeSpan Validity { get; init; }
	public bool StoreNull { get; init; }

	public Cache(Func<TKey, TValue> fetcher, TimeSpan validity, bool storeNull = false) {
		this.cache = new Dictionary<TKey, (TValue Data, DateTimeOffset FetchTime)>();
		Fetcher = fetcher;
		Validity = validity;
		StoreNull = storeNull;
	}

	public TValue GetItem(TKey key) {
		if (cache.ContainsKey(key)) {
			if (cache[key].FetchTime + Validity > DateTimeOffset.Now) {
				return cache[key].Data;
			}
			else {
				cache.Remove(key);
			}
		}

		var data = Fetcher(key);
		if (data != null) {
			cache[key] = (data, DateTimeOffset.Now);
		}
		return data;
	}
}

public class AsyncCache<TKey, TValue> where TKey: notnull {
	private readonly IDictionary<TKey, (TValue Data, DateTimeOffset FetchTime)> cache;

	public Func<TKey, Task<TValue>> Fetcher { get; init; }
	public TimeSpan Validity { get; init; }
	public bool StoreNull { get; init; }

	public AsyncCache(Func<TKey, Task<TValue>> fetcher, TimeSpan validity, bool storeNull = false) {
		this.cache = new Dictionary<TKey, (TValue Data, DateTimeOffset FetchTime)>();
		Fetcher = fetcher;
		Validity = validity;
		StoreNull = storeNull;
	}

	public async Task<TValue> GetItem(TKey key) {
		if (cache.ContainsKey(key)) {
			if (cache[key].FetchTime + Validity > DateTimeOffset.Now) {
				return cache[key].Data;
			}
			else {
				cache.Remove(key);
			}
		}

		var data = await Fetcher(key);
		if (data != null) {
			cache[key] = (data, DateTimeOffset.Now);
		}
		return data;
	}
}
