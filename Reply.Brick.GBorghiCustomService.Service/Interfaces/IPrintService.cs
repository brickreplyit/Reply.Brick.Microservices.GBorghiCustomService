using Reply.Brick.Messages.Messages.FromEventManager;
using System.Threading.Tasks;

namespace Reply.Brick.GBorghiCustomService.Service.Interfaces
{
    public interface IPrintService
    {
        Task PrintLabel(BrickEventIntegrationEvent message);
    }
}
