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
    public class PageModifier
    {
        private readonly WebView2 _webView;
        private readonly List<IPageModification> _modifications = new List<IPageModification>();
        private bool _isInitialized = false;

        public PageModifier(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            RegisterDefaultModifications();
        }

        /// <summary>
        /// Регистрация модификаций по умолчанию
        /// </summary>
        private void RegisterDefaultModifications()
        {
            // 1. Удаление атрибута style у .content.svelte-19qvtly (чтобы картинки открывались в большем разрешении)
            _modifications.Add(new RemoveStyleModification(
                ".content.svelte-19qvtly",
                "style"
            ));

            // 2. Удаление баннера .infobar.svelte-1aijhs3 (сносим рекламный баннер)
            _modifications.Add(new RemoveElementModification(
                ".infobar.svelte-1aijhs3",
                "setInterval"
            ));
        }

        /// <summary>
        /// Инициализация всех модификаций (вызывается при создании WebView)
        /// </summary>
        public async Task InitializeModificationsAsync()
        {
            if (_isInitialized) return;

            foreach (var modification in _modifications)
            {
                await modification.ApplyOnDocumentCreatedAsync(_webView.CoreWebView2);
            }

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[PageModifier] Инициализировано {_modifications.Count} модификаций");
        }

        /// <summary>
        /// Применение модификаций после загрузки страницы
        /// </summary>
        public async Task ApplyModificationsOnNavigationAsync()
        {
            if (!_isInitialized)
            {
                await InitializeModificationsAsync();
            }

            foreach (var modification in _modifications)
            {
                await modification.ApplyOnNavigationAsync(_webView.CoreWebView2);
            }
        }

        /// <summary>
        /// Добавление новой модификации во время выполнения
        /// </summary>
        public void AddModification(IPageModification modification)
        {
            _modifications.Add(modification);
            System.Diagnostics.Debug.WriteLine($"[PageModifier] Добавлена модификация: {modification.Name}");
        }

        /// <summary>
        /// Удаление модификации
        /// </summary>
        public bool RemoveModification(string name)
        {
            var mod = _modifications.Find(m => m.Name == name);
            if (mod != null)
            {
                _modifications.Remove(mod);
                System.Diagnostics.Debug.WriteLine($"[PageModifier] Удалена модификация: {name}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Получение всех модификаций
        /// </summary>
        public IReadOnlyList<IPageModification> GetModifications() => _modifications.AsReadOnly();
    }

    /// <summary>
    /// Интерфейс для модификации страницы
    /// </summary>
    public interface IPageModification
    {
        string Name { get; }
        string Description { get; }
        bool IsEnabled { get; set; }

        /// <summary>
        /// Применяется при создании документа (через AddScriptToExecuteOnDocumentCreatedAsync)
        /// </summary>
        Task ApplyOnDocumentCreatedAsync(CoreWebView2 coreWebView);

        /// <summary>
        /// Применяется при навигации (через ExecuteScriptAsync)
        /// </summary>
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
                    function removeAttributeFromElement() {{
                        var element = document.querySelector('{_selector}');
                        if (element && element.hasAttribute('{_attribute}')) {{
                            element.removeAttribute('{_attribute}');
                            console.log('[MaxLight] Удален атрибут {_attribute} из {_selector}');
                            return true;
                        }}
                        return false;
                    }}
                    
                    // Запускаем сразу
                    setTimeout(removeAttributeFromElement, 50);
                    
                    // Наблюдатель за изменениями
                    var observer = new MutationObserver(function() {{
                        removeAttributeFromElement();
                    }});
                    
                    document.addEventListener('DOMContentLoaded', function() {{
                        observer.observe(document.body, {{
                            childList: true,
                            subtree: true,
                            attributes: true,
                            attributeFilter: ['{_attribute}']
                        }});
                    }});
                    
                    // Автоматическое отключение через 30 секунд
                    setTimeout(function() {{
                        observer.disconnect();
                    }}, 30000);
                    
                    console.log('[MaxLight] Установлена модификация: {Name}');
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
                        console.log('[MaxLight] Удален атрибут {_attribute} при навигации');
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
                            console.log('[MaxLight] Удален элемент: {_selector}');
                            return true;
                        }}
                        return false;
                    }}
                    
                    if ('{_executionType}' === 'setInterval') {{
                        // Для баннера с setInterval
                        setInterval(function() {{
                            removeElement();
                        }}, 100);
                    }} else {{
                        setTimeout(removeElement, 50);
                        document.addEventListener('DOMContentLoaded', removeElement);
                    }}
                    
                    console.log('[MaxLight] Установлена модификация: {Name}');
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
                        console.log('[MaxLight] Удален элемент при навигации: {_selector}');
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

        public async Task ApplyOnNavigationAsync(CoreWebView2 coreWebView)
        {
            if (!IsEnabled) return;
            await coreWebView.ExecuteScriptAsync(_script);
        }
    }
}