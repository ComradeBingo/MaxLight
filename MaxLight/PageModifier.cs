using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaxLight
{
    /// <summary>
    /// Управляет модификациями HTML/JS страницы в WebView2
    /// </summary>
    public class PageModifier : IDisposable
    {
        private readonly WebView2 _webView;
        private readonly List<IPageModification> _modifications = new List<IPageModification>();
        private bool _isInitialized = false;
        private bool _disposed = false;

        public PageModifier(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            RegisterDefaultModifications();

            _webView.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
        }

        private void OnCoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                _ = InterceptClicksViaJavaScriptAsync();
            }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            try
            {
                var uri = new Uri(e.Uri);
                bool isOwnDomain =
                    uri.Host.EndsWith("max.ru", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith("oneme.ru", StringComparison.OrdinalIgnoreCase);

                e.Handled = true;

                if (isOwnDomain)
                {
                    // Файловый эндпоинт MAX (fd.oneme.ru/getfile?...) — не выкидываем
                    // во внешний браузер, а навигируем в том же WebView2.
                    // Тогда штатно сработает CoreWebView2.DownloadStarting.
                    _webView.CoreWebView2.Navigate(e.Uri);
                }
                else
                {
                    // Реально внешняя ссылка — как и раньше, открываем в браузере по умолчанию
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = e.Uri,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PageModifier] Ошибка открытия ссылки: {ex.Message}");
            }
        }

        private void RegisterDefaultModifications()
        {
            _modifications.Add(new RemoveStyleModification(".content.svelte-19qvtly", "style"));
            _modifications.Add(new RemoveElementModification(".infobar.svelte-1aijhs3", "setInterval"));
        }

        public async Task InitializeModificationsAsync()
        {
            if (_isInitialized) return;

            foreach (var modification in _modifications)
            {
                try
                {
                    await modification.ApplyOnDocumentCreatedAsync(_webView.CoreWebView2);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PageModifier] Ошибка инициализации {modification.Name}: {ex.Message}");
                }
            }

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[PageModifier] Инициализировано {_modifications.Count} модификаций");
        }

        public async Task ApplyModificationsOnNavigationAsync()
        {
            if (_disposed) return;

            if (!_isInitialized)
            {
                await InitializeModificationsAsync();
            }

            foreach (var modification in _modifications)
            {
                if (_disposed) break;

                try
                {
                    if (_webView.CoreWebView2 != null)
                    {
                        await modification.ApplyOnNavigationAsync(_webView.CoreWebView2);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PageModifier] Ошибка применения {modification.Name}: {ex.Message}");
                }
            }

            if (_webView.CoreWebView2 != null)
            {
                await InterceptClicksViaJavaScriptAsync();
            }
        }

        private async Task InterceptClicksViaJavaScriptAsync()
        {
            if (_webView.CoreWebView2 == null) return;

            try
            {
                string script = @"
                    (function() {
                        if (window.__maxlight_link_handler_installed) return;
                        
                        function openInBrowser(url) {
                            if (url && !url.startsWith('javascript:') && !url.startsWith('#')) {
                                window.open(url, '_blank');
                                return true;
                            }
                            return false;
                        }
                        
                        document.addEventListener('click', function(e) {
                            var target = e.target;
                            while (target && target.tagName !== 'A') {
                                target = target.parentElement;
                            }
                            
                            if (target && target.tagName === 'A') {
                                var href = target.getAttribute('href');
                                if (href && !href.startsWith('#') && !href.startsWith('javascript:')) {
                                    if (openInBrowser(href)) {
                                        e.preventDefault();
                                        e.stopPropagation();
                                    }
                                }
                            }
                        }, true);
                        
                        var originalWindowOpen = window.open;
                        window.open = function(url, name, features) {
                            if (url && typeof url === 'string' && 
                                !url.startsWith('javascript:') && 
                                !url.startsWith('#')) {
                                openInBrowser(url);
                                return null;
                            }
                            return originalWindowOpen.call(this, url, name, features);
                        };
                        
                        window.__maxlight_link_handler_installed = true;
                    })();
                ";

                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PageModifier] Ошибка перехвата ссылок: {ex.Message}");
            }
        }

        public void AddModification(IPageModification modification)
        {
            _modifications.Add(modification);
        }

        public bool RemoveModification(string name)
        {
            var mod = _modifications.Find(m => m.Name == name);
            if (mod != null)
            {
                _modifications.Remove(mod);
                return true;
            }
            return false;
        }

        public IReadOnlyList<IPageModification> GetModifications() => _modifications.AsReadOnly();

        public void Dispose()
        {
            if (_disposed) return;

            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
            }
            _webView.CoreWebView2InitializationCompleted -= OnCoreWebView2InitializationCompleted;

            _modifications.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// Интерфейс для модификации страницы
    /// </summary>
    public interface IPageModification
    {
        string Name { get; }
        string Description { get; }
        bool IsEnabled { get; set; }
        Task ApplyOnDocumentCreatedAsync(CoreWebView2 coreWebView);
        Task ApplyOnNavigationAsync(CoreWebView2 coreWebView);
    }

    /// <summary>
    /// Модификация для удаления атрибута у элемента
    /// </summary>
    public class RemoveStyleModification : IPageModification
    {
        public string Name { get; } = "RemoveStyleAttribute";
        public string Description { get; } = "Удаляет атрибут style у указанного элемента";
        public bool IsEnabled { get; set; } = true;

        private readonly string _selector;
        private readonly string _attribute;

        public RemoveStyleModification(string selector, string attribute)
        {
            _selector = selector;
            _attribute = attribute;
        }

        public async Task ApplyOnDocumentCreatedAsync(CoreWebView2 coreWebView)
        {
            if (!IsEnabled) return;

            string script = $@"
                (function() {{
                    function removeAttribute() {{
                        var element = document.querySelector('{_selector}');
                        if (element && element.hasAttribute('{_attribute}')) {{
                            element.removeAttribute('{_attribute}');
                        }}
                    }}
                    
                    setTimeout(removeAttribute, 50);
                    document.addEventListener('DOMContentLoaded', removeAttribute);
                }})();
            ";

            await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }

        public async Task ApplyOnNavigationAsync(CoreWebView2 coreWebView)
        {
            if (!IsEnabled) return;

            string script = $@"
                (function() {{
                    var element = document.querySelector('{_selector}');
                    if (element && element.hasAttribute('{_attribute}')) {{
                        element.removeAttribute('{_attribute}');
                    }}
                }})();
            ";

            await coreWebView.ExecuteScriptAsync(script);
        }
    }

    /// <summary>
    /// Модификация для удаления элемента
    /// </summary>
    public class RemoveElementModification : IPageModification
    {
        public string Name { get; } = "RemoveElement";
        public string Description { get; } = "Удаляет элемент с указанным селектором";
        public bool IsEnabled { get; set; } = true;

        private readonly string _selector;
        private readonly string _executionType;

        public RemoveElementModification(string selector, string executionType = "DOMContentLoaded")
        {
            _selector = selector;
            _executionType = executionType;
        }

        public async Task ApplyOnDocumentCreatedAsync(CoreWebView2 coreWebView)
        {
            if (!IsEnabled) return;

            string script = $@"
                (function() {{
                    function removeElement() {{
                        var element = document.querySelector('{_selector}');
                        if (element) {{
                            element.remove();
                            return true;
                        }}
                        return false;
                    }}
                    
                    if ('{_executionType}' === 'setInterval') {{
                        removeElement();
                        var intervalId = setInterval(function() {{
                            if (removeElement()) {{
                                clearInterval(intervalId);
                            }}
                        }}, 100);
                        setTimeout(function() {{ clearInterval(intervalId); }}, 30000);
                    }} else {{
                        setTimeout(removeElement, 50);
                        document.addEventListener('DOMContentLoaded', removeElement);
                    }}
                }})();
            ";

            await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }

        public async Task ApplyOnNavigationAsync(CoreWebView2 coreWebView)
        {
            if (!IsEnabled) return;

            string script = $@"
                (function() {{
                    var element = document.querySelector('{_selector}');
                    if (element) {{
                        element.remove();
                    }}
                }})();
            ";

            await coreWebView.ExecuteScriptAsync(script);
        }
    }

    /// <summary>
    /// Модификация для выполнения произвольного скрипта
    /// </summary>
    public class CustomScriptModification : IPageModification
    {
        public string Name { get; } = "CustomScript";
        public string Description { get; } = "Выполняет пользовательский скрипт";
        public bool IsEnabled { get; set; } = true;

        private readonly string _script;
        private readonly string _name;

        public CustomScriptModification(string script, string name = null)
        {
            _script = script;
            _name = name ?? $"CustomScript_{Guid.NewGuid():N}";
        }

        public async Task ApplyOnDocumentCreatedAsync(CoreWebView2 coreWebView)
        {
            if (!IsEnabled) return;
            await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(_script);
        }

        public async Task ApplyOnNavigationAsync(CoreWebView2 webView)
        {
            if (!IsEnabled) return;
            await webView.ExecuteScriptAsync(_script);
        }
    }
}