﻿// <copyright file="ConnectServerService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.AdminPanelBlazor.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using log4net;
    using MUnique.OpenMU.DataModel.Configuration;
    using MUnique.OpenMU.Interfaces;
    using MUnique.OpenMU.Persistence;

    public class ConnectServerService
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IList<IManageableServer> servers;
        private readonly IPersistenceContextProvider persistenceContextProvider;
        private readonly IServerConfigurationChangeListener changeListener;

        public ConnectServerService(IList<IManageableServer> servers, IPersistenceContextProvider persistenceContextProvider, IServerConfigurationChangeListener changeListener)
        {
            this.servers = servers;
            this.persistenceContextProvider = persistenceContextProvider;
            this.changeListener = changeListener;
        }

        public IEnumerable<GameClientDefinition> GetClients()
        {
            using var configContext = this.persistenceContextProvider.CreateNewContext();
            return configContext.Get<GameClientDefinition>();
        }

        public void Delete(IConnectServer connectServer)
        {
            try
            {
                Log.Warn($"User requested to delete connect server {connectServer.Id}.");
                if (connectServer.Settings is ConnectServerDefinition settings)
                {
                    using var configContext = this.persistenceContextProvider.CreateNewContext();
                    var success = configContext.Delete(settings);
                    if (success)
                    {
                        connectServer.Shutdown();
                        this.servers.Remove(connectServer);
                        Log.Warn($"User successfully deleted connect server {connectServer.Id}.");
                    }
                }
                else
                {
                    Log.Error($"User couldn't delete connect server {connectServer.Id}, id {connectServer.Settings.GetId()}.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"An unexpected error occured during the request to delete connect server {connectServer.Id}.", e);
                throw;
            }
        }

        public void Save(ConnectServerDefinition configuration)
        {
            try
            {
                Log.Info($"requested to save configuration of connect server {configuration.ServerId}");
                DataModel.Configuration.ConnectServerDefinition currentConfiguration = null;
                if (this.servers.OfType<IConnectServer>().FirstOrDefault(s => s.Settings.GetId() == configuration.GetId()) is IConnectServer server)
                {
                    currentConfiguration = server.Settings as DataModel.Configuration.ConnectServerDefinition;
                }

                bool isNew = false;

                using (var configContext = this.persistenceContextProvider.CreateNewContext())
                {
                    if (currentConfiguration != null)
                    {
                        configContext.Attach(currentConfiguration);
                    }
                    else
                    {
                        currentConfiguration = configContext.CreateNew<DataModel.Configuration.ConnectServerDefinition>();
                        currentConfiguration.ServerId = configuration.ServerId;
                        var existingConfigs = configContext.Get<DataModel.Configuration.ConnectServerDefinition>().ToList();
                        if (existingConfigs.Count > 0)
                        {
                            var newId = existingConfigs.Max(c => c.ServerId) + 1;
                            currentConfiguration.ServerId = (byte)newId;
                        }

                        isNew = true;
                    }

                    // todo: use automapper / mapster
                    currentConfiguration.CheckMaxConnectionsPerAddress = configuration.CheckMaxConnectionsPerAddress;
                    currentConfiguration.ListenerBacklog = configuration.ListenerBacklog;
                    currentConfiguration.MaxConnections = configuration.MaxConnections;
                    currentConfiguration.MaxConnectionsPerAddress = configuration.MaxConnectionsPerAddress;
                    currentConfiguration.MaxFtpRequests = configuration.MaxFtpRequests;
                    currentConfiguration.MaxIpRequests = configuration.MaxIpRequests;
                    currentConfiguration.MaximumReceiveSize = configuration.MaximumReceiveSize;
                    currentConfiguration.MaxServerListRequests = configuration.MaxServerListRequests;
                    currentConfiguration.ClientListenerPort = configuration.ClientListenerPort;
                    currentConfiguration.CurrentPatchVersion = configuration.CurrentPatchVersion;
                    currentConfiguration.Timeout = configuration.Timeout;
                    currentConfiguration.PatchAddress = configuration.PatchAddress;
                    currentConfiguration.Description = configuration.Description;
                    currentConfiguration.DisconnectOnUnknownPacket = configuration.DisconnectOnUnknownPacket;
                    if (!Equals(currentConfiguration.Client, configuration.Client))
                    {
                        currentConfiguration.Client = configContext.GetById<DataModel.Configuration.GameClientDefinition>(configuration.Client.GetId());
                    }

                    configContext.SaveChanges();
                }

                if (isNew)
                {
                    this.changeListener?.ConnectionServerAdded(currentConfiguration);
                }
                else
                {
                    this.changeListener?.ConnectionServerChanged(currentConfiguration);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"An unexpected exception occured during saving the connect server configuration for server id {configuration.ServerId}", ex);
                throw;
            }
        }
    }
}