namespace AsvTest;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();;
            };

            var app = new AsvApp();
            try
            {
                await app.RunAsync(cts.Token).ConfigureAwait(false);
                return 0;
            }
            finally
            {
                await ((IAsyncDisposable)app).DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }
}