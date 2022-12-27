using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Server.Utils; 

public record IAsyncCusorAsyncEnumerator<T>(IAsyncCursor<T> Cursor) {
    private IEnumerator<T>? enumerator = null;

    public T Current => enumerator!.Current;

    public async Task<bool> MoveNextAsync() {
        bool result;
        if (enumerator != null) {
            result = enumerator.MoveNext();
            if (result) return true;
        }

        result = await Cursor.MoveNextAsync();
        if (result) {
            enumerator = Cursor.Current.GetEnumerator();
            return true;
        }

        return false;
    }
}

public static class IAsyncCursorExtensions {
    public static IAsyncCusorAsyncEnumerator<T> GetAsyncEnumerator<T>(this IAsyncCursor<T> cursor) {
        return new(cursor);
    }
}