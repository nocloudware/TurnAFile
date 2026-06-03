using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TurnAFile.Windows.Services;

/// <summary>
/// Gestiona instancia única de la aplicación mediante mutex + named pipe.
///
/// Flujo cuando el usuario selecciona 3 archivos en Explorer
/// y elige "Agregar a TurnAFile":
///
///   Explorer llama 3 veces: TurnAFile.exe --add "a.mp4"
///                           TurnAFile.exe --add "b.mp4"
///                           TurnAFile.exe --add "c.mp4"
///
///   1ª llamada ? no hay mutex ? se convierte en instancia principal.
///                Abre MainWindow, inicia servidor de pipe,
///                espera 300ms por más archivos antes de cargar.
///
///   2ª y 3ª   ? detectan mutex existente ? conectan al pipe,
///                envían su ruta, se cierran solas.
///
///   Resultado: una sola ventana con los 3 archivos cargados.
///
/// Si el usuario repite la acción con otros archivos más tarde:
///   ? La instancia ya está corriendo ? se agregan al listado existente.
/// </summary>
public static class SingleInstanceManager
{
    private const string MutexName = @"Global\TurnAFile_SingleInstance";
    private const string PipeName = "TurnAFileIPC";
    private const string EndOfMessage = "<EOF>";

    // Tiempo de espera para agrupar archivos de una misma selección múltiple
    private const int BatchWindowMs = 350;

    private static Mutex? _mutex;
    private static bool _isFirstInstance;

    // ---------------------------------------------------------------------
    // API pública
    // ---------------------------------------------------------------------

    /// <summary>
    /// Intenta adquirir el mutex de instancia única.
    /// Retorna true si esta es la primera instancia (debe arrancar normalmente).
    /// Retorna false si ya hay una instancia corriendo.
    /// </summary>
    public static bool TryBecomeFirstInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out _isFirstInstance);
        return _isFirstInstance;
    }

    /// <summary>
    /// Envía una ruta de archivo a la instancia ya corriendo.
    /// Llamado por instancias secundarias antes de cerrarse.
    /// </summary>
    public static void SendFileToRunningInstance(string filePath)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.Out, PipeOptions.None);

            // Timeout corto: si la instancia no responde, no bloquear
            pipe.Connect(timeout: 2000);

            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine(filePath);
            writer.WriteLine(EndOfMessage);
            writer.Flush();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SingleInstance pipe write error: {ex.Message}");
        }
    }

    /// <summary>
    /// Inicia el servidor de pipe en background.
    /// Cada vez que llegan archivos, invoca onFilesReceived en el dispatcher.
    /// </summary>
    public static void StartPipeServer(Action<string[]> onFilesReceived)
    {
        Task.Run(() => PipeServerLoop(onFilesReceived));
    }

    /// <summary>
    /// Libera el mutex al cerrar la aplicación.
    /// </summary>
    public static void Release()
    {
        if (_isFirstInstance)
        {
            try { _mutex?.ReleaseMutex(); }
            catch { /* ignorar si ya fue liberado */ }
        }
        _mutex?.Dispose();
        _mutex = null;
    }

    // ---------------------------------------------------------------------
    // Servidor de pipe
    // ---------------------------------------------------------------------

    private static async void PipeServerLoop(Action<string[]> onFilesReceived)
    {
        while (true)
        {
            try
            {
                // Esperar conexión entrante
                using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync();

                using var reader = new StreamReader(pipe, Encoding.UTF8);

                // Leer archivos hasta EOF
                var batch = new System.Collections.Generic.List<string>();
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line == EndOfMessage) break;
                    if (!string.IsNullOrWhiteSpace(line))
                        batch.Add(line.Trim('"'));
                }

                if (batch.Count > 0)
                {
                    // Esperar la ventana de batch para agrupar llamadas
                    // simultáneas de Explorer (una por archivo seleccionado)
                    await Task.Delay(BatchWindowMs);

                    // Drenar mensajes adicionales que pudieron llegar
                    // durante la espera (conexiones extra en cola)
                    // — el loop vuelve a aceptar en la próxima iteración

                    onFilesReceived(batch.ToArray());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SingleInstance pipe server error: {ex.Message}");
                await Task.Delay(1000); // Pausa antes de reintentar
            }
        }
    }
}