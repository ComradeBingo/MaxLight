
(function () {
    console.log('=== Max Light: Парсер сообщений v7.3 ===');

    // Кеш отправленных сообщений
    var sentMessages = new Map();
    var isWindowActive = true; // ПО УМОЛЧАНИЮ АКТИВНО (пока C# не скажет иначе)
    var observer = null;
    var lastNotificationTime = 0;
    var notificationCooldown = 2000;

    // Функция для обновления состояния из C#
    window.updateWindowActiveState = function (isActive) {
        isWindowActive = isActive;
        console.log(isActive ? '🟢 Окно приложения активно - уведомления ОТКЛЮЧЕНЫ' : '🔴 Окно приложения неактивно - уведомления ВКЛЮЧЕНЫ');
    };

    // Отправка уведомления в C#
    function sendNotification(title, body, avatarUrl) {
        try {
            var now = Date.now();
            if (now - lastNotificationTime < notificationCooldown) {
                console.log('⏸️ Антиспам: уведомление отклонено');
                return;
            }
            lastNotificationTime = now;

            var maxLength = 1000;
            var displayBody = body;
            if (displayBody && displayBody.length > maxLength) {
                displayBody = displayBody.substring(0, maxLength) + '...';
            }

            window.chrome.webview.postMessage(JSON.stringify({
                type: 'notification',
                title: title,
                body: displayBody,
                avatar: avatarUrl || '',
                timestamp: now
            }));
            console.log('✅ Уведомление отправлено:', title, 'в', new Date().toLocaleTimeString());
        } catch (e) {
            console.log('❌ Ошибка отправки:', e);
        }
    }

    // Получение аватарки пользователя
    function getAvatarUrl(chatElement) {
        try {
            var avatarImg = chatElement.querySelector('img.avatarImage.svelte-1aizpza');
            if (avatarImg && avatarImg.src) {
                var avatarUrl = avatarImg.src;
                console.log('🖼️ Найдена аватарка:', avatarUrl);
                return avatarUrl;
            }

            var anyAvatar = chatElement.querySelector('.avatar img, [class*="avatar"] img');
            if (anyAvatar && anyAvatar.src) {
                console.log('🖼️ Найдена аватарка (альт):', anyAvatar.src);
                return anyAvatar.src;
            }

            return null;
        } catch (e) {
            console.log('Ошибка получения аватарки:', e);
            return null;
        }
    }

    // Получение имени контакта
    function getContactName(chatElement) {
        try {
            var nameSpan = chatElement.querySelector('.text.svelte-1riu5uh');
            if (nameSpan) {
                var name = nameSpan.innerText.trim();
                name = name.replace(/<!---->/g, '').trim();
                if (name) return name;
            }

            var anyTextSpan = chatElement.querySelector('span[class*="text"]');
            if (anyTextSpan) {
                var altName = anyTextSpan.innerText.trim();
                if (altName && altName.length < 50 && altName.length > 1) return altName;
            }

            return null;
        } catch (e) {
            return null;
        }
    }

    // Получение текста последнего сообщения
    function getLastMessageText(chatElement) {
        try {
            var messageSpan = chatElement.querySelector('.text.svelte-q2jdqb');
            if (messageSpan) {
                var text = messageSpan.innerText.trim();
                text = text.replace(/<!---->/g, '').trim();
                if (text) return text;
            }

            var anyMessageSpan = chatElement.querySelector('span[class*="text"]:not([class*="riu5uh"])');
            if (anyMessageSpan) {
                var altText = anyMessageSpan.innerText.trim();
                if (altText) return altText;
            }

            return null;
        } catch (e) {
            return null;
        }
    }

    // Проверка на непрочитанное сообщение
    function hasUnreadMessage(chatElement) {
        try {
            var badge = chatElement.querySelector('.badgeIcon.badgeIcon--20.badgeIcon--themed-regular');
            return badge !== null;
        } catch (e) {
            return false;
        }
    }

    // Получение информации о чате
    function getChatInfo(chatElement) {
        try {
            if (window._tokenParserActive) {
                console.log('⚠️ Парсер токенов активен, пропускаем обработку чата');
                return null;
            }

            var name = getContactName(chatElement);
            var message = getLastMessageText(chatElement);
            var hasUnread = hasUnreadMessage(chatElement);
            var avatar = getAvatarUrl(chatElement);

            if (!name || !message) return null;

            return {
                name: name,
                message: message,
                hasUnread: hasUnread,
                avatar: avatar,
                element: chatElement
            };
        } catch (e) {
            return null;
        }
    }

    // Генерация ID сообщения
    function getMessageId(name, message) {
        if (!name || !message) return null;

        var hash = 0;
        var str = name + message;
        for (var i = 0; i < str.length; i++) {
            hash = ((hash << 5) - hash) + str.charCodeAt(i);
            hash = hash & hash;
        }

        return 'msg_' + Math.abs(hash);
    }

    // Поиск всех чатов
    function findAllChats() {
        var contentContainer = document.querySelector('.content.svelte-1u8ha7t');
        if (!contentContainer) {
            console.log('⚠️ Контейнер .content.svelte-1u8ha7t не найден');
            return [];
        }

        
        var chatSelectors = [
            '.dialog',
            '.chat-item',
            '.cell',
            '[class*="dialog"]',
            '[class*="chat"]',
            '.conversation',
            '.contact'
        ];

        var chats = [];
        for (var i = 0; i < chatSelectors.length; i++) {
            var found = contentContainer.querySelectorAll(chatSelectors[i]);
            for (var j = 0; j < found.length; j++) {
                if (chats.indexOf(found[j]) === -1) {
                    chats.push(found[j]);
                }
            }
        }

        if (chats.length === 0) {
            chats = Array.from(contentContainer.children);
        }

        console.log(`🔍 Найдено чатов: ${chats.length}`);
        return chats;
    }

    // Обработка нового сообщения
    function processNewChat(chatElement) {
        try {
            if (window._tokenParserActive) {
                return false;
            }

            var chatInfo = getChatInfo(chatElement);
            if (!chatInfo || !chatInfo.name || !chatInfo.message) return false;

            if (!chatInfo.hasUnread) {
                return false;
            }

            var msgId = getMessageId(chatInfo.name, chatInfo.message);
            if (!msgId) return false;

            if (sentMessages.has(msgId)) {
                console.log(`🔄 Дубль сообщения от ${chatInfo.name}`);
                return false;
            }

            sentMessages.set(msgId, Date.now());

            console.log(`📨 НОВОЕ непрочитанное сообщение от ${chatInfo.name}`);
            console.log(`   Текст: ${chatInfo.message.substring(0, 50)}`);

            cleanOldMessages();

            // Отправляем уведомление ТОЛЬКО если окно НЕ активно
            if (!isWindowActive) {
                sendNotification(chatInfo.name, chatInfo.message, chatInfo.avatar);
                return true;
            } else {
                console.log(`   ⏸️ Окно активно, уведомление не отправлено`);
            }

            return false;
        } catch (e) {
            console.log('❌ Ошибка обработки чата:', e);
            return false;
        }
    }

    // Очистка старых сообщений из кеша
    function cleanOldMessages() {
        var now = Date.now();
        var deletedCount = 0;

        for (var [key, time] of sentMessages.entries()) {
            if (now - time > 86400000) {
                sentMessages.delete(key);
                deletedCount++;
            }
        }

        if (sentMessages.size > 1000) {
            var toDelete = Array.from(sentMessages.keys()).slice(0, 500);
            for (var i = 0; i < toDelete.length; i++) {
                sentMessages.delete(toDelete[i]);
                deletedCount++;
            }
        }

        if (deletedCount > 0) {
            console.log(`🧹 Очищено кэша: ${deletedCount} (осталось: ${sentMessages.size})`);
        }
    }

    // Инициализация кеша существующими непрочитанными сообщениями
    function initCache() {
        if (window._tokenParserActive) {
            console.log('⚠️ Парсер токенов активен, пропускаем инициализацию кеша');
            return;
        }

        var chats = findAllChats();
        var unreadCount = 0;
        var totalCount = 0;

        for (var i = 0; i < chats.length; i++) {
            var chatInfo = getChatInfo(chats[i]);
            if (chatInfo && chatInfo.name && chatInfo.message) {
                totalCount++;
                if (chatInfo.hasUnread) {
                    unreadCount++;
                    var msgId = getMessageId(chatInfo.name, chatInfo.message);
                    if (msgId && !sentMessages.has(msgId)) {
                        sentMessages.set(msgId, Date.now());
                    }
                }
            }
        }

        console.log(`📦 Кеш загружен: ${sentMessages.size} сообщений (всего чатов: ${totalCount}, непрочитанных: ${unreadCount})`);
    }

    // Периодическое сканирование
    function startPeriodicScan() {
        if (window._tokenParserActive || window._maxLightTokenInterceptorInstalled) {
            console.log('⚠️ Парсер токенов активен, пропускаем периодическое сканирование');
            return;
        }

        setInterval(function () {
            if (!isWindowActive) {
                var chats = findAllChats();
                for (var i = 0; i < chats.length; i++) {
                    processNewChat(chats[i]);
                }
            }
        }, 3000);
    }

    // Настройка MutationObserver
    function setupObserver() {
        if (window._tokenParserActive || window._maxLightTokenInterceptorInstalled) {
            console.log('⚠️ Парсер токенов активен, пропускаем настройку MutationObserver');
            return;
        }

        var contentContainer = document.querySelector('.content.svelte-1u8ha7t');
        if (!contentContainer) {
            console.log('⚠️ Контейнер не найден, повторная попытка через 1 секунду');
            setTimeout(setupObserver, 1000);
            return;
        }

        if (observer) {
            observer.disconnect();
        }

        observer = new MutationObserver(function (mutations) {
            if (window._tokenParserActive || window._maxLightTokenInterceptorInstalled) {
                return;
            }

            mutations.forEach(function (mutation) {
                if (mutation.addedNodes && mutation.addedNodes.length > 0) {
                    mutation.addedNodes.forEach(function (node) {
                        if (node.nodeType === 1) {
                            processNewChat(node);
                            if (node.querySelector && node.querySelector('.badgeIcon')) {
                                console.log('🔔 Обнаружен бейдж непрочитанного сообщения');
                                var chatElement = target.closest('.dialog, .chat-item, .cell, [class*="dialog"], [class*="chat"]');
                                if (chatElement) {
                                    processNewChat(chatElement);
                                }
                            }
                        }
                    });
                }

                if (mutation.type === 'attributes' && mutation.target) {
                    var target = mutation.target;
                    if (target.classList && target.classList.contains('badgeIcon')) {
                        console.log('🔔 Появился бейдж непрочитанного сообщения');
                        var chatElement = target.closest('.dialog, .chat-item, .cell, [class*="dialog"], [class*="chat"]');
                        if (chatElement) {
                            processNewChat(chatElement);
                        }
                    }
                }
            });
        });

        observer.observe(contentContainer, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['class', 'style']
        });

        console.log('👁️ MutationObserver запущен');
    }

    // Отслеживание входящих звонков
    function setupCallObserver() {
        console.log('📞 Настройка отслеживания входящих звонков...');

        function handleIncomingCall(callElement) {
            console.log('📞 ВХОДЯЩИЙ ЗВОНОК ОБНАРУЖЕН!');
            try {
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'incoming_call',
                    timestamp: Date.now()
                }));
                console.log('✅ Уведомление о звонке отправлено в C#');
            } catch (e) {
                console.log('❌ Ошибка отправки уведомления о звонке:', e);
            }
        }

        function checkForIncomingCall() {
            var callElement = document.querySelector('.call--incoming.play.svelte-1tedpg9');
            if (!callElement) {
                callElement = document.querySelector('.call--incoming');
            }
            if (!callElement) {
                callElement = document.querySelector('[class*="call--incoming"]');
            }

            if (callElement && !callElement.hasAttribute('data-maxlight-processed')) {
                callElement.setAttribute('data-maxlight-processed', 'true');
                handleIncomingCall(callElement);
                return true;
            }
            return false;
        }

        setTimeout(checkForIncomingCall, 1000);
        setTimeout(checkForIncomingCall, 3000);
        setTimeout(checkForIncomingCall, 5000);
        setInterval(checkForIncomingCall, 1000);

        var observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                if (mutation.addedNodes && mutation.addedNodes.length > 0) {
                    mutation.addedNodes.forEach(function (node) {
                        if (node.nodeType === 1) {
                            if (node.matches && node.matches('.call--incoming.play.svelte-1tedpg9')) {
                                handleIncomingCall(node);
                            }
                            if (node.querySelector) {
                                var callElement = node.querySelector('.call--incoming.play.svelte-1tedpg9');
                                if (callElement && !callElement.hasAttribute('data-maxlight-processed')) {
                                    callElement.setAttribute('data-maxlight-processed', 'true');
                                    handleIncomingCall(callElement);
                                }
                            }
                        }
                    });
                }
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });

        console.log('✅ Отслеживание звонков запущено');
    }

    // Инициализация
    function init() {
        console.log('🚀 Инициализация парсера уведомлений v7.3...');

        if (window._tokenParserActive) {
            console.log('⚠️ Парсер токенов активен, отключаем парсер уведомлений');
            return;
        }

        var checkInterval = setInterval(function () {
            var container = document.querySelector('.content.svelte-1u8ha7t');
            if (container) {
                clearInterval(checkInterval);
                console.log('✅ Контейнер найден, запуск парсера');
                initCache();
                setupObserver();
                startPeriodicScan();
                setInterval(cleanOldMessages, 60000);
                console.log('=== Парсер запущен ===');
                console.log('📌 Текущее состояние окна: ' + (isWindowActive ? 'Активно' : 'Неактивно'));
            }
        }, 500);
    }

    // Запуск
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Запускаем отслеживание звонков
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupCallObserver);
    } else {
        setupCallObserver();
    }
})();