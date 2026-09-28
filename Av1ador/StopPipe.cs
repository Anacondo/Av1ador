using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace Av1ador
{
    // Tiny control channel: the running (single) instance listens on a named pipe,
    // and "av1ador.exe --stop-encode" just sends one line to it and exits.
    internal static class StopPipe
    {
        private const string PipeName = "Av1ador.control";   // per-machine name; the pipe is only reachable by the same user by default

        // Client side: returns true if the running instance received the command.
        public static bool Send(string command, int timeoutMs = 2000)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(timeoutMs);
                    byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                    client.Write(data, 0, data.Length);
                    client.Flush();
                    return true;
                }
            }
            catch
            {
                return false;   // no running instance, or it did not answer
            }
        }

        // Server side: call once from the main form. onCommand runs on a background thread.
        public static void Listen(Action<string> onCommand)
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte))
                        {
                            server.WaitForConnection();
                            using (var reader = new StreamReader(server, Encoding.UTF8))
                            {
                                string line = reader.ReadLine();
                                if (!string.IsNullOrWhiteSpace(line))
                                    onCommand(line.Trim());
                            }
                        }
                    }
                    catch
                    {
                        Thread.Sleep(500);   // never let the listener die
                    }
                }
            });
            thread.IsBackground = true;   // does not keep the process alive
            thread.Start();
        }
    }
}
