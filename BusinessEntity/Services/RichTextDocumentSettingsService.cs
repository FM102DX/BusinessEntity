using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.Services;
using BusinessEntity.Settings;
using Microsoft.Extensions.Options;

namespace BusinessEntity.Services
{
    public class RichTextDocumentSettingsService
    {
        private readonly BusinessEntityHelper _businessEntityHelper;
        private readonly IOptions<RichTextDocumentSettings> _fallbackSettings;

        public RichTextDocumentSettingsService(
            BusinessEntityHelper businessEntityHelper,
            IOptions<RichTextDocumentSettings> fallbackSettings)
        {
            _businessEntityHelper = businessEntityHelper;
            _fallbackSettings = fallbackSettings;
        }

        public async Task<RichTextDocumentSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            var fallback = _fallbackSettings.Value;
            var sysParametersEntity = await _businessEntityHelper.GetOrCreateSingletonEntityAsync<SysParameters>(
                BusinessEntityTypeEnum.SysParametersTp,
                "SysParameters",
                cancellationToken);

            var sysParameters = sysParametersEntity.Data;
            return new RichTextDocumentSettings
            {
                InitialChunkCount = sysParameters.RichTextInitialChunkCount <= 0
                    ? fallback.InitialChunkCount
                    : sysParameters.RichTextInitialChunkCount,
                TableOfContentsBeforeBuffer = sysParameters.RichTextTableOfContentsBeforeBuffer < 0
                    ? fallback.TableOfContentsBeforeBuffer
                    : sysParameters.RichTextTableOfContentsBeforeBuffer,
                TableOfContentsAfterBuffer = sysParameters.RichTextTableOfContentsAfterBuffer < 0
                    ? fallback.TableOfContentsAfterBuffer
                    : sysParameters.RichTextTableOfContentsAfterBuffer,
                ScrollPreviousChunkCount = sysParameters.RichTextScrollPreviousChunkCount < 0
                    ? fallback.ScrollPreviousChunkCount
                    : sysParameters.RichTextScrollPreviousChunkCount,
                HideTableOfContentsScrollbar = sysParameters.RichTextHideTableOfContentsScrollbar
            };
        }
    }
}
