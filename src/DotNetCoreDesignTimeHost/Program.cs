//
// Program.cs
//
// Author:
//       Matt Ward <matt.ward@gmail.com>
//
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.DotNet.ProjectModel.Server.Models;
using Newtonsoft.Json.Linq;

namespace DotNetCoreDesignTimeHost
{
    class Program
    {
        static DesignTimeHostManager host;
        static string hostId;
        static string solutionPath;
        static ProcessingQueue queue;

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                ShowUsage();
                return;
            }

            solutionPath = args[0];
            Run();
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: DotNetCoreDesignTimeHost solutionPath");
        }

        static void Run()
        {
            hostId = Guid.NewGuid().ToString();
            host = new DesignTimeHostManager();
            host.Start(hostId, port => OnConnected(port));

            WaitForQuit();

            host.Stop();
        }

        static void OnConnected(int port)
        {
            Console.WriteLine("OnConnected. Port {0}", port);

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(IPAddress.Loopback, port));

            var networkStream = new NetworkStream(socket);

            Console.WriteLine("Connected");

            queue = new ProcessingQueue(networkStream);

            queue.OnReceive += m =>
            {
               // var project = Projects[m.ContextId];

                if (m.MessageType == "ProjectInformation")
                {
                    var val = m.Payload.ToObject<ProjectInformationMessage>();
                }
                else if (m.MessageType == "References")
                {
                    // References as well as the dependency graph information
                    var val = m.Payload.ToObject<ReferencesMessage>();
                }
                else if (m.MessageType == "Dependencies")
                {
                    var val = m.Payload.ToObject<DependenciesMessage>();
                }
                else if (m.MessageType == "DependencyDiagnostics")
                {
                    var val = m.Payload.ToObject<DiagnosticsListMessage>();
                }
                else if (m.MessageType == "CompilerOptions")
                {
                    // Configuration and compiler options
                    var val = m.Payload.ToObject<CompilationOptionsMessage>();
                }
                else if (m.MessageType == "Sources")
                {
                    var val = m.Payload.ToObject<SourcesMessage>();
                }
                else if (m.MessageType == "Diagnostics")
                {
                    var val = m.Payload.ToObject<DiagnosticsListMessage>();
                }
                else if (m.MessageType == "Error")
                {
                    var val = m.Payload.ToObject<ErrorMessage>();
                }
            };

            queue.Start();

            InitializeProjects();
        }

        static void InitializeProjects()
        {
            int projectContextId = 0;
            foreach (string projectFile in Directory.EnumerateFiles(solutionPath, "project.json", SearchOption.AllDirectories))
            {
                projectContextId++;

                string projectFolder = Path.GetDirectoryName(projectFile);

                var initializeMessage = new InitializeMessage
                {
                    ProjectFolder = projectFolder
                };

                queue.Post(Message.FromPayload(
                    "Initialize",
                    projectContextId,
                    JToken.FromObject(initializeMessage)));

                var changeConfigMessage = new ChangeConfigurationMessage
                {
                    Configuration = "Release"
                };

                queue.Post(Message.FromPayload(
                    "ChangeConfiguration",
                    projectContextId,
                    JToken.FromObject(changeConfigMessage)));
            }
        }

        static void WaitForQuit()
        {
            Console.WriteLine("Press Q to quit.");

            while(true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Q)
                {
                    return;
                }
            }
        }
   }
}
