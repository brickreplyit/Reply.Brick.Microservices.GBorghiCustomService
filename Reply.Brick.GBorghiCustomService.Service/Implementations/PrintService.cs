using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reply.Brick.GBorghiCustomService.Service.Interfaces;
using Reply.Brick.Infrastructure.CommonDTO.EventManager;
using Reply.Brick.Infrastructure.Interfaces.Services;
using Reply.Brick.Messages.Messages.FromEventManager;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reply.Brick.GBorghiCustomService.Service.Implementations
{
    public class PrintService : IPrintService
    {
        #region Variables
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IMessageService _messageService;

        protected const string _configKey = "EntityConfiguration_";
        protected double _TTLMinutes = 480;
        #endregion

        #region Constructor
        public PrintService(ILogger<PrintService> logger,
                            IConfiguration configuration,
                            IMessageService messageService)
        {
            _logger = logger;
            _configuration = configuration;
            _messageService = messageService;
        }
        #endregion

        public async Task PrintLabel(BrickEventIntegrationEvent message)
        {
            #region Variable
            EntityMetadata entityToPrint = new EntityMetadata();
            String templateXP50x60 = "XP_50x60";
            String templateXF60x60 = "XF_60x60";
            #endregion

            try
            {
                #region Initial Check
                EntityMetadata entity = message.BrickEvent.entities.FirstOrDefault();
                if (entity == null)
                {
                    throw new Exception("None Entity found");
                }

                bool isLastOp = entity.GetMetadata<bool>("isLastOp");
                if (!isLastOp)
                {
                    _logger.LogInformation("Is not the last operation");
                    return;
                }

                String orderCode = entity.GetMetadata("orderCode");
                if (String.IsNullOrEmpty(orderCode))
                {
                    throw new Exception("Metadata orderCode not found");
                }

                var conf = await _messageService.GetProductionEntityConfigurations(new List<string>() { orderCode }, message.PlantId);
                if (conf == null || conf.Count == 0)
                {
                    throw new Exception("Configuration not found - OrderNumber");
                }

                bool isDisassebly = conf.FirstOrDefault().OrderType == Infrastructure.Enums.OrderType.UTO;
                if (isDisassebly)
                {
                    _logger.LogInformation("Is UTO order");
                    return;
                }

                int okQty = entity.GetMetadata<int>("okQty");
                if (okQty <= 0)
                {
                    throw new Exception($"Declared okQty is {okQty} - None make created");
                }

                String itemCode = entity.GetMetadata("itemCode");
                if (String.IsNullOrEmpty(itemCode))
                {
                    throw new Exception("Metadata itemCode not found");
                }

                String itemCodeDescription = entity.GetMetadata("itemDescription_it-IT");
                if (String.IsNullOrEmpty(itemCodeDescription))
                {
                    throw new Exception("Metadata itemDescription_it-IT not found");
                }

                String cd_marconf = entity.GetMetadata("cd_marconf");
                if (String.IsNullOrEmpty(cd_marconf))
                {
                    throw new Exception("Metadata cd_marconf-IT not found");
                }
                String pnProduzione = entity.GetMetadata("PN_PRODUZIONE");
                String xRef = entity.GetMetadata("X-REF");
                String lineId = entity.GetMetadata("lineId");
                #endregion

                #region Common data
                String data = message.BrickEvent.eventDate.ToString("yyyy-MM-dd");
                entityToPrint.InsertUpdateMetadata("{DATA}", data);

                if (!string.IsNullOrEmpty(lineId))
                {
                    entityToPrint.InsertUpdateMetadata("lineId", lineId);

                }
                String WHS = "71";
                String SupplierCode = "80001";
                #endregion

                if (cd_marconf == "70")
                {
                    #region 60x60
                    String barcode1 = "";
                    String barcode2 = "";
                    String itemCodePadded = "";
                    String xRefPadded = "";
                    String partNumberVisible = "";
                    String xRefVisble = "";

                    if (itemCode[0].ToString().ToUpper() == "K")
                    {
                        if (String.IsNullOrEmpty(xRef))
                        {
                            //Ho la K e NON ho X-REF
                            //Popolare solo il secondo barcode
                            String itemCodeToPadd = itemCode.Substring(1, itemCode.Length).ToString();
                            itemCodePadded = itemCodeToPadd.Length > 10 ?
                                               itemCodeToPadd.Substring(0, 10) :
                                               itemCodeToPadd.PadLeft(10, '0');
                            barcode2 = itemCodePadded + "001";

                            xRefVisble = itemCodePadded + " - 001";
                        }
                        else
                        {
                            //Ho la K ed ho X-REF
                            //Nel primo barcode ci metto x-ref, e nel secondo ci metto il PN senza la K ed inserisco un padding se necessario

                            String itemCodeToPadd = itemCode.Substring(1, itemCode.Length).ToString();
                            itemCodePadded = itemCodeToPadd.Length > 10 ?
                                                itemCodeToPadd.Substring(0, 10) :
                                                itemCodeToPadd.PadLeft(10, '0');
                            barcode2 = itemCodePadded + "001";

                            partNumberVisible = itemCodePadded + " - 001";


                            xRefPadded = xRef.Length > 10 ?
                                                xRef.Substring(0, 10) :
                                                xRef.PadLeft(10, '0');
                            barcode1 = xRefPadded + "001";

                            xRefVisble = xRefPadded + " - 001";
                        }
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(xRef))
                        {
                            //NON ho la K e NON ho X-REF
                            //Non ho il secondo barcode
                            itemCodePadded = itemCode.Length > 10 ?
                                               itemCode.Substring(0, 10) :
                                               itemCode.PadLeft(10, '0');
                            barcode1 = itemCodePadded + "001";

                            partNumberVisible = itemCodePadded.Substring(1, 10) + " - 001";
                        }
                        else
                        {
                            //NON ho la K ed ho X-REF 
                            itemCodePadded = itemCode.Length > 10 ?
                                                itemCode.Substring(0, 10) :
                                                itemCode.PadLeft(10, '0');
                            barcode1 = itemCodePadded + "001";

                            partNumberVisible = itemCodePadded + " - 001";


                            xRefPadded = xRef.Length > 10 ?
                                                xRef.Substring(0, 10) :
                                                xRef.PadLeft(10, '0');
                            barcode2 = xRefPadded + "001";

                            xRefVisble = xRefPadded + " - 001";
                        }
                    }

                    entityToPrint.InsertUpdateMetadata("{BARCODE1}", barcode1);
                    entityToPrint.InsertUpdateMetadata("{BARCODE2}", barcode2);
                    entityToPrint.InsertUpdateMetadata("{PARTNUMBER}", partNumberVisible);
                    entityToPrint.InsertUpdateMetadata("{XREF}", xRefVisble);

                    await _messageService.RequestPrintLabel(message.PlantId, message.BrickEvent.user, templateXF60x60, new List<EntityMetadata>() { entityToPrint });
                    #endregion
                }
                else
                {
                    #region 50x60
                    String pnDesc = itemCodeDescription.Substring(0, Math.Min(30, itemCodeDescription.Length));
                    entityToPrint.InsertUpdateMetadata("{PNDESC}", pnDesc);

                    String barcode = "";
                    if (String.IsNullOrEmpty(pnProduzione))
                    {
                        barcode = "P" + itemCode;
                        entityToPrint.InsertUpdateMetadata("{BARCODE}", barcode);

                        itemCode = FormatStringByGroups(itemCode);
                        entityToPrint.InsertUpdateMetadata("{PARTNUMBER}", itemCode);
                    }
                    else
                    {
                        barcode = "P" + pnProduzione;
                        entityToPrint.InsertUpdateMetadata("{BARCODE}", barcode);

                        pnProduzione = FormatStringByGroups(pnProduzione);
                        entityToPrint.InsertUpdateMetadata("{PARTNUMBER}", pnProduzione);
                    }


                    await _messageService.RequestPrintLabel(message.PlantId, message.BrickEvent.user, templateXP50x60, new List<EntityMetadata>() { entityToPrint });
                    #endregion
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        #region Private Methods
        private String FormatStringByGroups(string inputString)
        {
            if (string.IsNullOrEmpty(inputString))
            {
                return string.Empty; // Handle empty input gracefully
            }

            int[] groupSizes = { 2, 3, 3, 2 }; // Define the desired group sizes
            int totalLength = groupSizes.Sum();

            string formattedString = inputString.Length > totalLength ?
                inputString.Substring(0, totalLength) : inputString.PadLeft(totalLength); // Handle longer or shorter strings

            StringBuilder outputBuilder = new StringBuilder();

            int currentGroupIndex = 0;
            int currentLenght = 0;
            for (int i = 0; i < formattedString.Length; i++)
            {
                outputBuilder.Append(formattedString[i]);
                currentLenght++;
                // Check for group end and update currentGroupIndex before appending space
                if (currentGroupIndex < groupSizes.Length)
                {
                    if ((currentLenght % groupSizes[currentGroupIndex]) == 0)
                    {
                        outputBuilder.Append(' ');
                        currentLenght = 0;
                        currentGroupIndex++;
                    }
                }
            }

            return outputBuilder.ToString().TrimEnd(); // Remove trailing space
        }

        private async Task<bool> CheckBoolean(String sBool)
        {
            try
            {
                bool varBool = false;
                var conversionOk = Boolean.TryParse(sBool, out varBool);
                if (!conversionOk)
                    return false;

                return varBool;
            }
            catch
            {
                return false;
            }
        }

        private async Task<int> CheckInt(string val)
        {
            bool quantityOk = double.TryParse(val.Replace(",", "."), NumberStyles.Float | NumberStyles.Number, CultureInfo.InvariantCulture, out double q);

            if (!quantityOk)
            {
                return 0;
            }
            else
            {
                // Forza conversione a intero
                return (int)q;
            }
        }

        private async Task<double> CheckDouble(string val)
        {
            bool quantityOk = double.TryParse(val.Replace(",", "."), NumberStyles.Float | NumberStyles.Number, CultureInfo.InvariantCulture, out double q);
            return q;
        }
        #endregion
    }
}