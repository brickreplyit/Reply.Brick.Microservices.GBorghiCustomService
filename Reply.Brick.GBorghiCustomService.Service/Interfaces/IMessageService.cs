using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reply.Brick.Infrastructure.Enums;
using Reply.Brick.Infrastructure.CommonDTO;
using Reply.Brick.Infrastructure.CommonDTO.EventManager;

namespace Reply.Brick.GBorghiCustomService.Service.Interfaces
{
    public interface IMessageService
    {
        #region ToNotification
        Task SendNotificationCommand(NotificationSource notificationSource, NotificationError notificationError,
                    Guid plantId, Guid? workstationId, String workstationName, String operationDescription, String apiName,
                    String alarmName, String alarmNote, String userId, String userName, String orderNumber, String partNumber,
                    String notMappedIdentifier, String plantName = null);
        #endregion

        #region ToConfigurationManager
        Task<List<ProductionEntityConfigurationDTO>> GetRoutingConfiguration(Guid plantId, List<Guid> partIds);
        Task<CustomConfigurationDTO> GetDefaultConfiguration(Guid plantId, ConfigurationType configurationType, OrderType orderType);
        Task<List<ProductionEntityConfigurationDTO>> GetProductionEntityConfigurations(List<string> orderNumbers, Guid plantId);
        #endregion

        #region ToPrint
        Task<bool> RequestPrintLabel(Guid plantId, String userId, String templateName, List<EntityMetadata> entities);
        #endregion
    }
}
