//
// DesignTimeHostManager.cs
//
// Author:
//       OmniSharp
//       Matt Ward <ward.matt@gmail.com>
//
// Copyright (c) 2015 OmniSharp
// Copyright (c) 2016 Matthew Ward
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DotNetCoreDesignTimeHost
{
    public class DesignTimeHostManager
    {
        private readonly object _processLock = new object();
        private Process _designTimeHostProcess;
        private bool _stopped;

        public TimeSpan DelayBeforeRestart { get; set; }

        public void Start(string hostId, Action<int> onConnected)
        {
            lock (_processLock)
            {
                if (_stopped)
                {
                    return;
                }

                int port = GetFreePort();

                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    Arguments = string.Format(@"projectmodel-server --port {0} --host-pid {1} --host-name {2}",
                                              port,
                                              Process.GetCurrentProcess().Id,
                                              hostId),
                };

                Console.WriteLine(psi.FileName + " " + psi.Arguments);

                _designTimeHostProcess = Process.Start(psi);

                // Wait a little bit for it to conncet before firing the callback
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    var t1 = DateTime.UtcNow;
                    var dthTimeout = TimeSpan.FromSeconds(10);
                    while (!socket.Connected && DateTime.UtcNow - t1 < dthTimeout)
                    {
                        Thread.Sleep(500);
                        try
                        {
                            socket.Connect(new IPEndPoint(IPAddress.Loopback, port));
                        }
                        catch (SocketException)
                        {
                            // this happens when the DTH isn't listening yet
                        }
                    }

                    if (!socket.Connected)
                    {
                        // reached timeout
                        Console.WriteLine("Failed to launch DesignTimeHost in a timely fashion.");
                        return;
                    }
                }

                if (_designTimeHostProcess.HasExited)
                {
                    // REVIEW: Should we quit here or retry?
                    Console.WriteLine(string.Format("Failed to launch DesignTimeHost. Process exited with code {0}.", _designTimeHostProcess.ExitCode));
                    return;
                }

                Console.WriteLine(string.Format("Running DesignTimeHost on port {0}, with PID {1}", port, _designTimeHostProcess.Id));

                _designTimeHostProcess.EnableRaisingEvents = true;
                _designTimeHostProcess.OnExit(() =>
                {
                    Console.WriteLine("Design time host process ended");

                    Start(hostId, onConnected);
                });

                onConnected(port);
            }
        }

        public void Stop()
        {
            lock (_processLock)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;

                if (_designTimeHostProcess != null)
                {
                    Console.WriteLine("Shutting down DesignTimeHost");

                    _designTimeHostProcess.KillAll();
                    _designTimeHostProcess = null;
                }
            }
        }

        private static int GetFreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
