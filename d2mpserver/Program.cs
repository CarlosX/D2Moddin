﻿using System;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Runtime.InteropServices;
using d2mpserver.Properties;
using log4net.Config;

namespace d2mpserver
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static volatile bool shutdown = false;

        private static bool ShutdownImmediately()
        {
            ShutdownAll();
            return true;
        }

        public static void ShutdownAll()
        {
            var conn = connection;
            connection = null;

            var man = manager;
            manager = null;

            if (conn != null)
                conn.Shutdown();
            if (man != null)
            {
                man.Shutdown();
            }
            shutdown = true;
        }

        static ServerConnection connection;
        static ServerManager manager;
        static void Main(string[] args)
        {
            var defaultConfig = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile + ".default";
            if (!File.Exists(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile) && File.Exists(defaultConfig))
            {
                File.Copy(defaultConfig, AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
                ServerUpdater.RestartD2MP();
                return;
            }

            Console.CancelKeyPress += (sender, arg) => ShutdownImmediately();

            XmlConfigurator.Configure();
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            
            log.Info("D2MP server version "+ServerUpdater.version+" starting...");
            log.Debug("Connection address: " + Settings.Default.serverIP);

            Console.Title = string.Format("[{0}] D2MP Server", ServerUpdater.version);

            manager = new ServerManager();
            if (!manager.SetupEnvironment())
            {
                log.Fatal("Failed to setup the server, exiting.");
                return;
            }

            connection = new ServerConnection(manager);
            if (!connection.Connect())
            {
                log.Fatal("Can't connect to the server!");
                return;
            }

            connection.StartServerThread();

            while (!shutdown)
            {
                Thread.Sleep(100);
            }
            ShutdownAll();
        }
    }
}
