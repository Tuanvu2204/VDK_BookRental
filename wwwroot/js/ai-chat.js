(() => {
    "use strict";

    /*
     * VDK Book Rental - AI Chat
     *
     * Chức năng:
     * - Gọi POST /api/chat.
     * - Tự hủy khi phản hồi quá lâu.
     * - Không cho gửi nhiều yêu cầu cùng lúc.
     * - Luôn xóa dấu ba chấm khi thành công hoặc thất bại.
     * - Giới hạn lịch sử để phản hồi nhanh hơn.
     * - Hiển thị lỗi rõ ràng thay vì quay vô hạn.
     */

    const API_URL = "/api/chat";

    // Frontend chờ tối đa 30 giây.
    const REQUEST_TIMEOUT_MS = 30000;

    // Chỉ gửi 6 tin nhắn gần nhất lên backend.
    const MAX_HISTORY_MESSAGES = 6;

    // Giới hạn độ dài câu hỏi.
    const MAX_MESSAGE_LENGTH = 1000;

    let chatHistory = [];
    let activeController = null;
    let isRequestRunning = false;

    let typingElement = null;
    let slowMessageTimer = null;
    let verySlowMessageTimer = null;

    /**
     * Tìm phần tử đầu tiên khớp một trong các selector.
     */
    function findElement(
        selectors,
        parent = document
    ) {
        for (const selector of selectors) {
            const element =
                parent.querySelector(selector);

            if (element) {
                return element;
            }
        }

        return null;
    }

    /*
     * Hỗ trợ nhiều tên id/class để không phụ thuộc
     * hoàn toàn vào một phiên bản giao diện.
     */

    const chatPanel = findElement([
        "#aiChatPanel",
        "#chatPanel",
        "#vdkAiChatPanel",
        ".ai-chat-panel",
        ".ai-chat-window",
        ".ai-chat-box",
        ".vdk-ai-chat-panel",
        "[data-ai-chat-panel]"
    ]);

    const messagesContainer = findElement([
        "#aiChatMessages",
        "#chatMessages",
        "#vdkAiChatMessages",
        ".ai-chat-messages",
        ".chat-messages",
        ".ai-chat-body",
        ".vdk-ai-chat-messages",
        "[data-ai-chat-messages]"
    ]);

    const messageInput = findElement([
        "#aiChatInput",
        "#chatInput",
        "#messageInput",
        "#vdkAiChatInput",
        ".ai-chat-input",
        ".chat-input",
        ".vdk-ai-chat-input",
        "textarea[placeholder*='Nhập câu hỏi']",
        "input[placeholder*='Nhập câu hỏi']",
        "[data-ai-chat-input]"
    ]);

    const chatForm =
        messageInput?.closest("form") ??
        findElement([
            "#aiChatForm",
            "#chatForm",
            "#vdkAiChatForm",
            ".ai-chat-form",
            ".vdk-ai-chat-form",
            "[data-ai-chat-form]"
        ]);

    const sendButton =
        chatForm
            ? findElement(
                [
                    "#aiChatSend",
                    "#chatSend",
                    "#sendButton",
                    "#sendChatButton",
                    ".ai-chat-send",
                    ".chat-send-button",
                    ".vdk-ai-chat-send",
                    "button[type='submit']",
                    "[data-ai-chat-send]"
                ],
                chatForm
            )
            : findElement([
                "#aiChatSend",
                "#chatSend",
                "#sendButton",
                "#sendChatButton",
                ".ai-chat-send",
                ".chat-send-button",
                ".vdk-ai-chat-send",
                "[data-ai-chat-send]"
            ]);

    const openButton = findElement([
        "#aiChatOpen",
        "#aiChatToggle",
        "#chatToggle",
        "#vdkAiChatOpen",
        ".ai-chat-open",
        ".ai-chat-toggle",
        ".ai-chat-launcher",
        ".ai-chat-fab",
        ".vdk-ai-chat-open",
        "[data-ai-chat-open]"
    ]);

    const closeButton = findElement([
        "#aiChatClose",
        "#chatClose",
        "#vdkAiChatClose",
        ".ai-chat-close",
        ".vdk-ai-chat-close",
        "[data-ai-chat-close]"
    ]);

    const minimizeButton = findElement([
        "#aiChatMinimize",
        "#chatMinimize",
        "#vdkAiChatMinimize",
        ".ai-chat-minimize",
        ".vdk-ai-chat-minimize",
        "[data-ai-chat-minimize]"
    ]);

    const resetButton = findElement([
        "#aiChatReset",
        "#chatReset",
        "#vdkAiChatReset",
        ".ai-chat-reset",
        ".vdk-ai-chat-reset",
        "[data-ai-chat-reset]"
    ]);

    /*
     * Không tiếp tục nếu không tìm thấy
     * những thành phần quan trọng.
     */
    if (!messagesContainer || !messageInput) {
        console.warn(
            "VDK AI Chat: Không tìm thấy vùng tin nhắn hoặc ô nhập."
        );

        return;
    }

    /**
     * Mã hóa HTML để tránh chèn mã độc.
     */
    function escapeHtml(value) {
        const temporaryElement =
            document.createElement("div");

        temporaryElement.textContent =
            value ?? "";

        return temporaryElement.innerHTML;
    }

    /**
     * Hiển thị một số Markdown cơ bản.
     */
    function formatMessageContent(value) {
        let safeValue =
            escapeHtml(value ?? "");

        // **chữ đậm**
        safeValue =
            safeValue.replace(
                /\*\*(.+?)\*\*/g,
                "<strong>$1</strong>"
            );

        // `code`
        safeValue =
            safeValue.replace(
                /`([^`]+)`/g,
                "<code>$1</code>"
            );

        // Xuống dòng.
        safeValue =
            safeValue.replace(
                /\r?\n/g,
                "<br>"
            );

        return safeValue;
    }

    /**
     * Cuộn xuống tin nhắn mới nhất.
     */
    function scrollToBottom() {
        window.requestAnimationFrame(() => {
            messagesContainer.scrollTop =
                messagesContainer.scrollHeight;
        });
    }

    /**
     * Tạo một tin nhắn.
     */
    function createMessageElement(
        role,
        content,
        options = {}
    ) {
        const {
            isError = false,
            generated = true
        } = options;

        const messageRow =
            document.createElement("div");

        const isUser =
            role === "user";

        messageRow.className = [
            "ai-chat-message",
            "ai-message",
            "vdk-ai-message",
            isUser
                ? "ai-chat-message-user"
                : "ai-chat-message-assistant",
            isUser
                ? "user"
                : "assistant",
            isUser
                ? "user-message"
                : "bot-message",
            isError
                ? "ai-chat-message-error"
                : ""
        ]
            .filter(Boolean)
            .join(" ");

        messageRow.dataset.role =
            role;

        if (generated) {
            messageRow.dataset.aiGenerated =
                "true";
        }

        const avatar =
            document.createElement("div");

        avatar.className = [
            "ai-chat-avatar",
            "ai-message-avatar",
            "vdk-ai-message-avatar"
        ].join(" ");

        avatar.textContent =
            isUser
                ? "Bạn"
                : "AI";

        const bubble =
            document.createElement("div");

        bubble.className = [
            "ai-chat-bubble",
            "ai-message-bubble",
            "vdk-ai-message-bubble"
        ].join(" ");

        bubble.innerHTML =
            formatMessageContent(content);

        if (isUser) {
            messageRow.appendChild(bubble);
        }
        else {
            messageRow.appendChild(avatar);
            messageRow.appendChild(bubble);
        }

        return messageRow;
    }

    /**
     * Thêm tin nhắn người dùng.
     */
    function addUserMessage(content) {
        const element =
            createMessageElement(
                "user",
                content
            );

        messagesContainer.appendChild(
            element
        );

        scrollToBottom();

        return element;
    }

    /**
     * Thêm tin nhắn AI.
     */
    function addAssistantMessage(
        content,
        isError = false
    ) {
        const element =
            createMessageElement(
                "assistant",
                content,
                {
                    isError
                }
            );

        messagesContainer.appendChild(
            element
        );

        scrollToBottom();

        return element;
    }

    /**
     * Xóa các timer thông báo chậm.
     */
    function clearTypingTimers() {
        if (slowMessageTimer) {
            window.clearTimeout(
                slowMessageTimer
            );

            slowMessageTimer = null;
        }

        if (verySlowMessageTimer) {
            window.clearTimeout(
                verySlowMessageTimer
            );

            verySlowMessageTimer = null;
        }
    }

    /**
     * Hiện dấu ba chấm đang trả lời.
     */
    function showTypingIndicator() {
        removeTypingIndicator();

        const row =
            document.createElement("div");

        row.className = [
            "ai-chat-message",
            "ai-message",
            "vdk-ai-message",
            "ai-chat-message-assistant",
            "assistant",
            "bot-message",
            "ai-chat-typing"
        ].join(" ");

        row.dataset.aiTyping =
            "true";

        const avatar =
            document.createElement("div");

        avatar.className = [
            "ai-chat-avatar",
            "ai-message-avatar",
            "vdk-ai-message-avatar"
        ].join(" ");

        avatar.textContent =
            "AI";

        const bubble =
            document.createElement("div");

        bubble.className = [
            "ai-chat-bubble",
            "ai-message-bubble",
            "vdk-ai-message-bubble",
            "ai-chat-typing-bubble"
        ].join(" ");

        const dots =
            document.createElement("span");

        dots.className =
            "ai-chat-typing-dots";

        dots.innerHTML = `
            <span class="ai-chat-typing-dot"></span>
            <span class="ai-chat-typing-dot"></span>
            <span class="ai-chat-typing-dot"></span>
        `;

        const status =
            document.createElement("span");

        status.className =
            "ai-chat-typing-status";

        status.textContent =
            "";

        status.style.fontSize =
            "11px";

        status.style.opacity =
            "0.7";

        status.style.marginLeft =
            "7px";

        bubble.appendChild(dots);
        bubble.appendChild(status);

        row.appendChild(avatar);
        row.appendChild(bubble);

        messagesContainer.appendChild(
            row
        );

        typingElement =
            row;

        /*
         * Cho người dùng biết ứng dụng vẫn đang xử lý,
         * thay vì chỉ quay ba chấm không rõ lý do.
         */
        slowMessageTimer =
            window.setTimeout(() => {
                if (typingElement) {
                    status.textContent =
                        "Đang kiểm tra dữ liệu sách...";
                }
            }, 7000);

        verySlowMessageTimer =
            window.setTimeout(() => {
                if (typingElement) {
                    status.textContent =
                        "Gemini đang phản hồi chậm...";
                }
            }, 17000);

        scrollToBottom();

        return row;
    }

    /**
     * Xóa dấu ba chấm.
     */
    function removeTypingIndicator() {
        clearTypingTimers();

        if (typingElement) {
            typingElement.remove();
            typingElement = null;
        }

        /*
         * Dọn thêm các indicator cũ trong trường hợp
         * yêu cầu trước bị lỗi bất thường.
         */
        messagesContainer
            .querySelectorAll(
                "[data-ai-typing='true']"
            )
            .forEach(element => {
                element.remove();
            });
    }

    /**
     * Bật hoặc tắt trạng thái đang gửi.
     */
    function setBusyState(isBusy) {
        isRequestRunning =
            isBusy;

        messageInput.disabled =
            isBusy;

        messageInput.setAttribute(
            "aria-busy",
            isBusy
                ? "true"
                : "false"
        );

        if (sendButton) {
            sendButton.disabled =
                isBusy;

            sendButton.setAttribute(
                "aria-busy",
                isBusy
                    ? "true"
                    : "false"
            );

            sendButton.classList.toggle(
                "is-loading",
                isBusy
            );
        }
    }

    /**
     * Đọc JSON hoặc nội dung lỗi từ server.
     */
    async function readResponsePayload(
        response
    ) {
        const responseText =
            await response.text();

        if (!responseText) {
            return {};
        }

        try {
            return JSON.parse(
                responseText
            );
        }
        catch {
            return {
                detail: responseText
            };
        }
    }

    /**
     * Lấy thông báo lỗi phù hợp.
     */
    function getErrorMessage(
        payload,
        statusCode
    ) {
        if (statusCode === 429) {
            return (
                "Trợ lý AI đang nhận quá nhiều yêu cầu. " +
                "Bạn vui lòng chờ một lát rồi thử lại."
            );
        }

        if (statusCode === 401 ||
            statusCode === 403) {
            return (
                "Yêu cầu đến Gemini chưa được xác thực. " +
                "Vui lòng kiểm tra cấu hình API key."
            );
        }

        if (statusCode >= 500) {
            return (
                payload?.detail ||
                payload?.title ||
                payload?.message ||
                "Máy chủ AI đang gặp sự cố. Vui lòng thử lại."
            );
        }

        return (
            payload?.detail ||
            payload?.title ||
            payload?.message ||
            `Không thể gửi câu hỏi. Mã lỗi: ${statusCode}.`
        );
    }

    /**
     * Gọi backend AI.
     */
    async function callChatApi(
        message,
        history
    ) {
        /*
         * Hủy request cũ nếu vẫn còn tồn tại.
         */
        if (activeController) {
            activeController.abort();
        }

        const controller =
            new AbortController();

        activeController =
            controller;

        let requestTimedOut =
            false;

        const timeoutId =
            window.setTimeout(() => {
                requestTimedOut = true;
                controller.abort();
            }, REQUEST_TIMEOUT_MS);

        try {
            const response =
                await fetch(
                    API_URL,
                    {
                        method: "POST",

                        headers: {
                            "Content-Type":
                                "application/json; charset=utf-8",

                            "Accept":
                                "application/json"
                        },

                        body: JSON.stringify({
                            message,
                            history
                        }),

                        signal:
                            controller.signal,

                        cache:
                            "no-store",

                        credentials:
                            "same-origin"
                    }
                );

            const payload =
                await readResponsePayload(
                    response
                );

            if (!response.ok) {
                throw new Error(
                    getErrorMessage(
                        payload,
                        response.status
                    )
                );
            }

            const reply =
                payload?.reply ??
                payload?.Reply;

            if (!reply ||
                typeof reply !== "string") {
                throw new Error(
                    "Máy chủ AI không trả về nội dung hợp lệ."
                );
            }

            return {
                ...payload,
                reply
            };
        }
        catch (error) {
            if (error?.name === "AbortError") {
                if (requestTimedOut) {
                    throw new Error(
                        "Trợ lý phản hồi quá lâu. " +
                        "Bạn hãy gửi lại câu hỏi ngắn hơn."
                    );
                }

                throw new Error(
                    "Yêu cầu AI đã được hủy."
                );
            }

            if (error instanceof TypeError) {
                throw new Error(
                    "Không thể kết nối tới máy chủ AI. " +
                    "Hãy kiểm tra website còn đang chạy."
                );
            }

            throw error;
        }
        finally {
            window.clearTimeout(
                timeoutId
            );

            if (activeController === controller) {
                activeController = null;
            }
        }
    }

    /**
     * Giới hạn lịch sử gửi đến server.
     */
    function getRecentHistory() {
        return chatHistory
            .slice(
                -MAX_HISTORY_MESSAGES
            )
            .map(item => ({
                role:
                    item.role,

                content:
                    item.content
            }));
    }

    /**
     * Giới hạn lịch sử lưu trong trình duyệt.
     */
    function trimLocalHistory() {
        const localHistoryLimit =
            MAX_HISTORY_MESSAGES * 2;

        if (chatHistory.length >
            localHistoryLimit) {
            chatHistory =
                chatHistory.slice(
                    -localHistoryLimit
                );
        }
    }

    /**
     * Gửi một câu hỏi.
     */
    async function sendMessage() {
        if (isRequestRunning) {
            return;
        }

        const message =
            messageInput.value.trim();

        if (!message) {
            messageInput.focus();
            return;
        }

        if (message.length >
            MAX_MESSAGE_LENGTH) {
            addAssistantMessage(
                `Câu hỏi tối đa ${MAX_MESSAGE_LENGTH} ký tự.`,
                true
            );

            messageInput.focus();
            return;
        }

        /*
         * Lịch sử phải là các tin nhắn trước câu hỏi hiện tại.
         * Không đưa câu hiện tại vào cả message và history.
         */
        const previousHistory =
            getRecentHistory();

        addUserMessage(
            message
        );

        chatHistory.push({
            role:
                "user",

            content:
                message
        });

        trimLocalHistory();

        messageInput.value =
            "";

        /*
         * Báo cho giao diện biết nội dung input đã đổi.
         */
        messageInput.dispatchEvent(
            new Event(
                "input",
                {
                    bubbles: true
                }
            )
        );

        setBusyState(
            true
        );

        showTypingIndicator();

        try {
            const result =
                await callChatApi(
                    message,
                    previousHistory
                );

            removeTypingIndicator();

            addAssistantMessage(
                result.reply
            );

            chatHistory.push({
                role:
                    "assistant",

                content:
                    result.reply
            });

            trimLocalHistory();
        }
        catch (error) {
            removeTypingIndicator();

            console.error(
                "VDK AI Chat Error:",
                error
            );

            addAssistantMessage(
                error?.message ||
                "Có lỗi xảy ra khi kết nối với trợ lý AI.",
                true
            );
        }
        finally {
            /*
             * Dù thành công hay lỗi đều phải:
             * - Xóa dấu ba chấm.
             * - Mở lại nút gửi.
             * - Mở lại ô nhập.
             */
            removeTypingIndicator();

            setBusyState(
                false
            );

            messageInput.focus();
        }
    }

    /**
     * Mở cửa sổ chat.
     */
    function openChatPanel() {
        if (!chatPanel) {
            return;
        }

        chatPanel.hidden =
            false;

        chatPanel.classList.add(
            "show",
            "open",
            "active",
            "is-open"
        );

        chatPanel.setAttribute(
            "aria-hidden",
            "false"
        );

        window.setTimeout(() => {
            messageInput.focus();
            scrollToBottom();
        }, 50);
    }

    /**
     * Đóng hoặc thu nhỏ cửa sổ chat.
     */
    function closeChatPanel() {
        if (!chatPanel) {
            return;
        }

        chatPanel.classList.remove(
            "show",
            "open",
            "active",
            "is-open"
        );

        chatPanel.setAttribute(
            "aria-hidden",
            "true"
        );

        /*
         * Không dùng display:none trực tiếp để CSS
         * hiện tại vẫn có thể chạy animation.
         */
        window.setTimeout(() => {
            const isStillClosed =
                !chatPanel.classList.contains(
                    "is-open"
                ) &&
                !chatPanel.classList.contains(
                    "open"
                ) &&
                !chatPanel.classList.contains(
                    "show"
                ) &&
                !chatPanel.classList.contains(
                    "active"
                );

            if (isStillClosed) {
                chatPanel.hidden =
                    true;
            }
        }, 200);
    }

    /**
     * Làm mới cuộc trò chuyện.
     */
    function resetConversation() {
        if (activeController) {
            activeController.abort();
            activeController = null;
        }

        setBusyState(
            false
        );

        removeTypingIndicator();

        chatHistory =
            [];

        /*
         * Chỉ xóa các tin nhắn được JavaScript thêm.
         * Tin chào mặc định trong Razor vẫn được giữ lại.
         */
        messagesContainer
            .querySelectorAll(
                "[data-ai-generated='true']"
            )
            .forEach(element => {
                element.remove();
            });

        messageInput.value =
            "";

        messageInput.focus();

        scrollToBottom();
    }

    /**
     * Gửi qua form.
     */
    if (chatForm) {
        chatForm.addEventListener(
            "submit",
            event => {
                event.preventDefault();
                void sendMessage();
            }
        );
    }

    /**
     * Trường hợp nút gửi không phải submit.
     */
    if (sendButton &&
        (!chatForm ||
            sendButton.type !== "submit")) {
        sendButton.addEventListener(
            "click",
            event => {
                event.preventDefault();
                void sendMessage();
            }
        );
    }

    /**
     * Enter để gửi.
     * Shift + Enter để xuống dòng nếu là textarea.
     */
    messageInput.addEventListener(
        "keydown",
        event => {
            if (event.key !== "Enter") {
                return;
            }

            if (event.shiftKey) {
                return;
            }

            event.preventDefault();

            void sendMessage();
        }
    );

    openButton?.addEventListener(
        "click",
        event => {
            event.preventDefault();
            openChatPanel();
        }
    );

    closeButton?.addEventListener(
        "click",
        event => {
            event.preventDefault();
            closeChatPanel();
        }
    );

    minimizeButton?.addEventListener(
        "click",
        event => {
            event.preventDefault();
            closeChatPanel();
        }
    );

    resetButton?.addEventListener(
        "click",
        event => {
            event.preventDefault();
            resetConversation();
        }
    );

    /*
     * Hủy request khi đóng hoặc tải lại trang.
     */
    window.addEventListener(
        "beforeunload",
        () => {
            activeController?.abort();
        }
    );

    messageInput.maxLength =
        MAX_MESSAGE_LENGTH;

    setBusyState(
        false
    );

    console.info(
        "VDK AI Chat đã khởi tạo thành công."
    );
})();