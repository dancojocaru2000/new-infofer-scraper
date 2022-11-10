using System;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Utils; 

// Inspired from: https://stackoverflow.com/a/57517920
public class AsyncThrottle {
    private readonly SemaphoreSlim openConnectionSemaphore;

    public AsyncThrottle(int limit) {
        openConnectionSemaphore = new(limit, limit);
    }

    public async Task<T> MakeRequest<T>(Task<T> task) => await MakeRequest(() => task);
    public async Task<T> MakeRequest<T>(Func<Task<T>> taskCreator) {
        await openConnectionSemaphore.WaitAsync();
        try {
            var result = await taskCreator();
            return result;
        }
        finally {
            openConnectionSemaphore.Release();
        }
    }
    
    
    public async Task MakeRequest(Task task) => await MakeRequest(() => task);
    public async Task MakeRequest(Func<Task> taskCreator) {
        await openConnectionSemaphore.WaitAsync();
        try {
            await taskCreator();
        }
        finally {
            openConnectionSemaphore.Release();
        }
    }
}