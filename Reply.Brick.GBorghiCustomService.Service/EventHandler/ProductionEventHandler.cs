using Reply.Brick.GBorghiCustomService.Service.Interfaces;
using Reply.Brick.Infrastructure.BaseEventHandler;
using Microsoft.Extensions.Logging;
using NServiceBus;
using Reply.Brick.Messages.Requests.ToERP;
using Reply.Brick.Infrastructure.Messages;
using System.Threading.Tasks;
using System;
using Reply.Brick.Messages.Requests.Response;
using Reply.Brick.Messages.Messages.FromEventManager;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Reply.Brick.GBorghiCustomService.Service.EventHandler
{
    public class ProductionEventHandler : BaseEventHandler,
        IHandleMessages<BrickEventIntegrationEvent>
    {
        #region Variables
        private readonly ILogger _logger;
        private readonly IPrintService _printService;
        private readonly IConfiguration _configuration;
        #endregion

        #region Constructor
        public ProductionEventHandler(ILogger<ProductionEventHandler> logger,
                                      IPrintService printService,
                                      IConfiguration configuration)
        {

            _logger = logger;
            _printService = printService;
            _configuration = configuration;
        }
        #endregion

        public async Task Handle(BrickEventIntegrationEvent message, IMessageHandlerContext context)
        {
            #region Intial checks
            if (message == null)
            {
                _logger.LogInformation("Handler BrickEventIntegrationEvent - Error: message is null");
                return;
            }

            if (message.BrickEvent == null)
            {
                _logger.LogInformation("Handler BrickEventIntegrationEvent - Error: BrickEvent is null");
                return;
            }
            #endregion

            try
            {
                List<String> plantToManage = _configuration.GetSection("PlantToManage").Get<List<String>>();
                if(!plantToManage.Contains(message.BrickEvent.plantCode))
                {
                    _logger.LogInformation($"Is not for me - EventId = {message.BrickEvent.eventId}");
                }

                switch (message.BrickEvent.eventType)
                {
                    case Infrastructure.Enums.EventTypeEnum.ProductionProgress:
                        await _printService.PrintLabel(message);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}
