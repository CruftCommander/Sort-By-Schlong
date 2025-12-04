using Microsoft.Extensions.DependencyInjection;
using SortBySchlong.Core.Exceptions;
using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Services;
using SortBySchlong.Core.Shapes;
using Serilog;
using IconArrangementService = SortBySchlong.Core.Services.IconArrangementService;

namespace SortBySchlong.ConsoleHarness;

/// <summary>
/// Console harness for testing and running the desktop icon arranger.
/// </summary>
internal class Program
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitInvalidArguments = 2;

    private static async Task<int> Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        try
        {
            // Parse command line arguments
            var shapeKey = "penis"; // Default shape
            var listShapes = false;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--shape" || arg == "-s")
                {
                    if (i + 1 < args.Length)
                    {
                        shapeKey = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --shape requires a value");
                        return ExitInvalidArguments;
                    }
                }
                else if (arg == "--list-shapes" || arg == "-l")
                {
                    listShapes = true;
                }
                else if (arg == "--help" || arg == "-h")
                {
                    PrintHelp();
                    return ExitSuccess;
                }
                else if (arg.StartsWith("--shape="))
                {
                    shapeKey = arg.Substring("--shape=".Length);
                }
                else
                {
                    Console.Error.WriteLine($"Error: Unknown argument: {arg}");
                    Console.Error.WriteLine("Use --help for usage information");
                    return ExitInvalidArguments;
                }
            }

            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();

            // Handle list shapes command
            if (listShapes)
            {
                var shapeRegistry = serviceProvider.GetRequiredService<IShapeRegistry>();
                var availableShapes = shapeRegistry.GetAvailableShapes();

                Console.WriteLine("Available shapes:");
                if (availableShapes.Count == 0)
                {
                    Console.WriteLine("  (no shapes registered)");
                }
                else
                {
                    foreach (var shape in availableShapes)
                    {
                        Console.WriteLine($"  - {shape}");
                    }
                }

                return ExitSuccess;
            }

            // Run the arrangement
            var arrangementService = serviceProvider.GetRequiredService<IconArrangementService>();
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\nCancellation requested...");
            };

            try
            {
                await arrangementService.ArrangeIconsAsync(shapeKey, cts.Token);
                Console.WriteLine($"Successfully arranged icons in '{shapeKey}' shape.");
                return ExitSuccess;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation was cancelled.");
                return ExitError;
            }
            catch (ShapeNotFoundException ex)
            {
                Console.Error.WriteLine($"Error: Shape '{ex.ShapeKey}' not found.");
                Console.Error.WriteLine($"Available shapes: {string.Join(", ", serviceProvider.GetRequiredService<IShapeRegistry>().GetAvailableShapes())}");
                return ExitError;
            }
            catch (IconArrangementException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"  {ex.InnerException.Message}");
                }

                Log.Error(ex, "Icon arrangement failed");
                return ExitError;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Log.Fatal(ex, "Fatal error occurred");
            return ExitError;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register logging
        services.AddSingleton<ILogger>(Log.Logger);

        // Register core services
        services.AddSingleton<IDesktopIconProvider, DesktopIconService>();
        services.AddSingleton<IIconLayoutApplier, DesktopIconService>();
        services.AddSingleton<IShapeRegistry, ShapeRegistry>();
        // Note: NoopShapeScriptEngine is optional and not currently used
        // services.AddSingleton<IShapeScriptEngine, NoopShapeScriptEngine>();

        // Register shape providers
        services.AddSingleton<IShapeProvider, PenisShapeProvider>();

        // Register orchestration service
        services.AddSingleton<IconArrangementService>();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Desktop Icon Arranger - Console Harness");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  SortBySchlong.ConsoleHarness [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --shape, -s <key>     Shape to arrange icons in (default: penis)");
        Console.WriteLine("  --shape=<key>         Alternative shape specification");
        Console.WriteLine("  --list-shapes, -l     List all available shapes");
        Console.WriteLine("  --help, -h            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  SortBySchlong.ConsoleHarness --shape=penis");
        Console.WriteLine("  SortBySchlong.ConsoleHarness --list-shapes");
    }
}
