using Reply.Brick.GBorghiCustomService.Service.Interfaces;
using Reply.Brick.Infrastructure.Enums;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NServiceBus;
using System;
using System.Threading.Tasks;

using Reply.Brick.Messages.Commands.Notification;
using System.Collections.Generic;
using Reply.Brick.Infrastructure.CommonDTO;
using Reply.Brick.Messages.Requests.ToConfigurationManager;
using Reply.Brick.Messages.Requests.Response;
using System.Threading;
using System.Linq;
using Reply.Brick.Infrastructure.CommonDTO.EventManager;
using Reply.Brick.Messages.Requests.ToPrint;
using Reply.Brick.Messages.Requests.Responses;
using MongoDB.Bson;
using Reply.Brick.Infrastructure.Interfaces.Services;

namespace Reply.Brick.GBorghiCustomService.Service.Implementations
{
    public class MessageService : IMessageService
    {
        #region Privates
        private readonly ILogger _logger;
        private IConfiguration _configuration;
        private readonly IEndpointInstance _bus;
        private readonly IBrickPersistanceLayer<ObjectId> _pl;

        private String _userConnectorId;
        private int _tokenDuration;
        protected const String _configKey = "Configuration_";
        protected double _TTLMinutes = 120;
        #endregion

        #region Constructor
        public MessageService(ILogger<MessageService> logger, IConfiguration configuration, IEndpointInstance bus,
                              IBrickPersistanceLayer<ObjectId> pl)
        {
            _logger = logger;
            _configuration = configuration;
            _bus = bus;
            _pl = pl;

            _userConnectorId = this._configuration.GetValue<String>("UserConnectorId");
            _tokenDuration = this._configuration.GetValue<int>("TokenDuration");
        }
        #endregion

        #region ToNotification
        public async Task SendNotificationCommand(NotificationSource notificationSource, NotificationError notificationError,
                    Guid plantId, Guid? workstationId, String workstationName, String operationDescription, String apiName,
                    String alarmName, String alarmNote, String userId, String userName, String orderNumber, String partNumber,
                    String notMappedIdentifier, String plantName = null)
        {

            try
            {
                ErrorNotificationCommand notification = new ErrorNotificationCommand()
                {
                    NotificationSource = notificationSource,
                    NotificationError = notificationError,
                    AlarmName = alarmName,
                    ApiName = apiName,
                    AlarmNote = alarmNote,
                    PlantId = plantId,
                    WorkstationId = workstationId,
                    WorkstationName = workstationName,
                    OperationDescription = operationDescription,
                    TimeStamp = DateTime.UtcNow,
                    System = _userConnectorId,
                    NotMappedIdentifier = notMappedIdentifier,
                    UserId = userId,
                    UserName = userName,
                    OrderName = orderNumber,
                    PartNumber = partNumber,
                    Entities = null,
                    PlantName = plantName
                };

                await _bus.Send(notification).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendNotificationCommand - Error: {ex.Message}");
            }
        }
        #endregion

        #region ToConfigurationManager
        public async Task<List<ProductionEntityConfigurationDTO>> GetRoutingConfiguration(Guid plantId, List<Guid> partIds)
        {
            try
            {
                RequestRoutingConfigurationMessage request = new RequestRoutingConfigurationMessage(plantId, partIds);
                var cancellationTokenS = new CancellationTokenSource(TimeSpan.FromSeconds(_tokenDuration));
                var response = await _bus.Request<ProductionEntityConfigurationResponseMessage>(request, cancellationTokenS.Token);
                
                if (response == null || 
                    response.ProductionEntityConfigurations == null || 
                    !response.ProductionEntityConfigurations.Any())
                {
                    throw new Exception("None Routing configuration found");
                }

                return response.ProductionEntityConfigurations;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return new List<ProductionEntityConfigurationDTO>();
            }
        }

        public async Task<CustomConfigurationDTO> GetDefaultConfiguration(Guid plantId, ConfigurationType configurationType, OrderType orderType)
        {
            try
            {
                RequestDefaultConfigurationMessage request = new RequestDefaultConfigurationMessage(plantId, configurationType, orderType);
                var cancellationTokenS = new CancellationTokenSource(TimeSpan.FromSeconds(_tokenDuration));
                var response = await _bus.Request<CustomConfigurationResponseMessage>(request, cancellationTokenS.Token);
                if (response == null ||
                    response.Configuration == null)
                {
                    throw new Exception("None Default configuration found");
                }

                return response.Configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return null;
            }
        }

        public async Task<List<ProductionEntityConfigurationDTO>> GetProductionEntityConfigurations(List<string> orderNumbers, Guid plantId)
        {
            List<ProductionEntityConfigurationDTO> config = new List<ProductionEntityConfigurationDTO>();

            try
            {
                #region Provo a leggere dalla cache
                List<String> keys = new List<string>();
                orderNumbers = orderNumbers.Distinct().ToList();
                orderNumbers.Distinct().ToList().ForEach(o => { keys.Add(String.Concat(_configKey, plantId, o)); });

                IEnumerable<ProductionEntityConfigurationDTO> configFromCache =
                    await _pl.Redis.GetByFilterAsync<ProductionEntityConfigurationDTO>(keys);

                List<string> orderNewNumbers = new List<string>();

                if (configFromCache == null || configFromCache.Count() == 0)
                {
                    orderNewNumbers = orderNumbers.ToList();
                }
                else
                {
                    orderNewNumbers = orderNumbers.Where(x => !configFromCache.Any(y => x == y.OrderNumber)).ToList();
                }
                #endregion

                #region Chiedo le configurazioni mancanti al Configuration Manager
                if (orderNewNumbers != null && orderNewNumbers.Count > 0)
                {
                    var cancellationTokenS = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                    var request = new RequestProductionEntityConfigurationByOrdersMessage { PlantId = plantId, OrderNumber = orderNewNumbers };
                    var response = (await _bus.Request<ProductionEntityConfigurationResponseMessage>(request, cancellationTokenS.Token).ConfigureAwait(false));
                    config = response.ProductionEntityConfigurations;

                    //salvo nella cache quelle che ho appena leto
                    if (config != null)
                    {
                        Dictionary<String, ProductionEntityConfigurationDTO> newconfig = new Dictionary<string, ProductionEntityConfigurationDTO>();
                        config.Distinct().ToList().ForEach(c => { newconfig.Add(String.Concat(_configKey, plantId, c.OrderNumber), c); });

                        await _pl.Redis.SetMultipleAsync<ProductionEntityConfigurationDTO>(newconfig, DateTime.UtcNow.AddMinutes(_TTLMinutes));
                    }
                }
                #endregion

                #region Aggiungo alla lista delle configurazioni che ho letto da cache
                if (configFromCache != null && configFromCache.Count() > 0)
                {
                    config.AddRange(configFromCache);
                }
                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, ex.Message);
            }
            return config;
        }
        #endregion

        #region ToPrint
        public async Task<bool> RequestPrintLabel(Guid plantId, String userId, String templateName, List<EntityMetadata> entities)
        {
            try
            {
                RequestPrintLabelMessage message = new RequestPrintLabelMessage(templateName, plantId, entities);
                message.UserId = userId;

                var cancellationTokenS = new CancellationTokenSource(TimeSpan.FromSeconds(_tokenDuration));
                var response = await _bus.Request<GeneralResponseMessage>(message, cancellationTokenS.Token);

                if(response == null ||
                   response.Result == null ||
                   !response.Result.Succeeded)
                {
                    if (response != null && response.Result != null )
                    {
                        throw new Exception(response.Result.ErrorMessage);
                    }

                    throw new Exception("General errod during comunication to Print MS");
                }

                return true;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return false;
            }
        }
        #endregion
    }
}