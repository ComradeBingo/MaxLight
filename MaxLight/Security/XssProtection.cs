using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace MaxLight.Security
{
    public static class XssProtection
    {
        public static async Task InjectProtectionScript(CoreWebView2 webView)
        {
            if (webView == null) return;

            string protectionScript = @"
        (function() {
            // Сохраняем оригинальные методы немедленно
            var originalPostMessage = window.chrome.webview.postMessage.bind(window.chrome.webview);
            var originalGetItem = localStorage.getItem.bind(localStorage);
            var originalSetItem = localStorage.setItem.bind(localStorage);
            
            // Защита postMessage через немедленную блокировку
            var allowedTypes = ['notification', 'auth_token_captured', 'device_id_captured', 'incoming_call'];
            
            window.chrome.webview.postMessage = function(message) {
                try {
                    var parsed = typeof message === 'string' ? JSON.parse(message) : message;
                    if (parsed && allowedTypes.includes(parsed.type)) {
                        return originalPostMessage(message);
                    }
                    console.warn('XSS: blocked message type', parsed?.type);
                } catch(e) {
                    console.warn('XSS: invalid message format');
                }
            };
            
            // Защита localStorage через defineProperty (неснимаемая)
            var protectedKeys = ['__oneme_auth', '__oneme_device_id'];
            
            Object.defineProperty(localStorage, 'getItem', {
                value: function(key) {
                    if (protectedKeys.includes(key) && isSuspicious()) {
                        console.warn('XSS: blocked getItem for', key);
                        return null;
                    }
                    return originalGetItem(key);
                },
                writable: false,
                configurable: false
            });
            
            Object.defineProperty(localStorage, 'setItem', {
                value: function(key, value) {
                    if (protectedKeys.includes(key) && isSuspicious()) {
                        console.warn('XSS: blocked setItem for', key);
                        return;
                    }
                    return originalSetItem(key, value);
                },
                writable: false,
                configurable: false
            });
            
            function isSuspicious() {
                try {
                    // Проверяем наличие необычных фреймов
                    if (window !== window.top) return true;
                    
                    // Проверяем, был ли скрипт выполнен из динамического кода
                    var stack = new Error().stack || '';
                    if (stack.includes('new Function') || 
                        stack.includes('eval') || 
                        stack.includes('apply')) {
                        return true;
                    }
                    
                    // Доп. проверка: атрибуты безопасности документа
                    if (document.querySelector('script[nonce]')) {
                        // Скорее всего CSP, скрипт легитимный
                        return false;
                    }
                    
                    return false;
                } catch(e) {
                    return true; // При ошибке — блокируем
                }
            }
            
            // Запечатываем объекты, чтобы нельзя было переопределить свойства обратно
            if (Object.seal) {
                Object.seal(localStorage);
                Object.seal(window.chrome?.webview);
            }
            
            console.log('XSS Protection v2: active');
        })();
    ";

            await webView.AddScriptToExecuteOnDocumentCreatedAsync(protectionScript);
        }
    }
}