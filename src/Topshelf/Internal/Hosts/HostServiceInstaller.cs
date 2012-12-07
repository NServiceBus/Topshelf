// Copyright 2007-2008 The Apache Software Foundation.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Topshelf.Internal.Hosts
{
    using System;
    using System.Collections;
    using System.Configuration.Install;
    using System.Reflection;
    using System.ServiceProcess;
    using Configuration;
    using log4net;
    using Microsoft.Win32;

    public class HostServiceInstaller :
        Installer
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof (HostServiceInstaller));
        private readonly ServiceInstaller _serviceInstaller = new ServiceInstaller();
        private readonly ServiceProcessInstaller _serviceProcessInstaller = new ServiceProcessInstaller();
        private readonly IRunConfiguration _config;

        public HostServiceInstaller(IRunConfiguration configuration)
        {
            _log.DebugFormat("Attempting to install with {0} configuration", configuration);
            _config = configuration;

            configuration.ConfigureServiceInstaller(_serviceInstaller);
            configuration.ConfigureServiceProcessInstaller(_serviceProcessInstaller);

            Installers.AddRange(new Installer[] {_serviceProcessInstaller, _serviceInstaller});            
        }


        public void Register()
        {
            if (!IsInstalled(_config))
            {
                using (TransactedInstaller ti = new TransactedInstaller())
                {
                    ti.Installers.Add(this);

                    string path = string.Format("/assemblypath={0}", Assembly.GetEntryAssembly().Location);

                    string[] commandLine = {path};

                    InstallContext context = new InstallContext(null, commandLine);
                    ti.Context = context;

                    Hashtable savedState = new Hashtable();

                    ti.Install(savedState);
                }
            }
            else
            {
                Console.WriteLine("Service is already installed");
                if (_log.IsInfoEnabled)
                    _log.Info("Service is already installed");
            }
        }

        public void Unregister()
        {
            if (IsInstalled(_config))
            {
                using (TransactedInstaller ti = new TransactedInstaller())
                {
                    ti.Installers.Add(this);

                    string path = string.Format("/assemblypath={0}", Assembly.GetEntryAssembly().Location);
                    string[] commandLine = {path};

                    InstallContext context = new InstallContext(null, commandLine);
                    ti.Context = context;

                    ti.Uninstall(null);
                }
            }
            else
            {
                Console.WriteLine("Service is not installed");
                if (_log.IsInfoEnabled)
                    _log.Info("Service is not installed");
            }
        }

        public static bool IsInstalled(IRunConfiguration configuration)
        {
            foreach (ServiceController service in ServiceController.GetServices())
            {
                if (service.ServiceName == configuration.WinServiceSettings.FullServiceName)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// For the .Net service install infrastructure
        /// </summary>
        /// <param name="stateSaver"></param>
        public override void Install(IDictionary stateSaver)
        {
            if (_log.IsInfoEnabled)
                _log.InfoFormat("Installing Service {0}", _serviceInstaller.ServiceName);

            switch (_config.WinServiceSettings.StartMode)
            {
                case ServiceStartMode.Manual:
                    _serviceInstaller.StartType = ServiceStartMode.Manual;
                    break;

                case ServiceStartMode.Automatic:
                    _serviceInstaller.StartType = ServiceStartMode.Automatic;

                    break;
                case ServiceStartMode.Disabled:
                    _serviceInstaller.StartType = ServiceStartMode.Disabled;

                    break;
            }

            base.Install(stateSaver);

            if (_log.IsDebugEnabled) _log.Debug("Opening Registry");

            using (RegistryKey system = Registry.LocalMachine.OpenSubKey("System"))
            using (RegistryKey currentControlSet = system.OpenSubKey("CurrentControlSet"))
            using (RegistryKey services = currentControlSet.OpenSubKey("Services"))
            using (RegistryKey service = services.OpenSubKey(_serviceInstaller.ServiceName, true))
            {
                service.SetValue("Description", _serviceInstaller.Description);

                string imagePath = (string)service.GetValue("ImagePath");

                _log.DebugFormat("Service Path {0}", imagePath);

                imagePath += _config.WinServiceSettings.InstanceName == null ?
                    " -service" : " -service -instance:{0}".FormatWith(_config.WinServiceSettings.InstanceName);

                if (!string.IsNullOrEmpty(_config.WinServiceSettings.CommandLine))
                {
                    imagePath += " " + _config.WinServiceSettings.CommandLine;
                }

                _log.DebugFormat("ImagePath '{0}'", imagePath);

                service.SetValue("ImagePath", imagePath);
            }

            if(_log.IsDebugEnabled) _log.Debug("Closing Registry");
        }
    }
}