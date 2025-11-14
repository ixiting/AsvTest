namespace AsvTest;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var app = new AsvApp();
            await app.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }
}