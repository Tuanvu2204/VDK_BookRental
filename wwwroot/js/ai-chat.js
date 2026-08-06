(() => {
    "use strict";

    const API_URL = "/api/chat";

    const REQUEST_TIMEOUT_MS = 30000;
    const MAX_HISTORY_MESSAGES = 6;
    const MAX_MESSAGE_LENGTH = 1000;

    const panel =
        document.getElementById("aiChatPanel");

    const openButton =
        document.getElementById("aiChatOpen");

    const closeButton =
        document.getElementById("aiChatClose");

    const minimizeButton =
        document.getElementById("aiChatMinimize");

    const resetButton =
        document.getElementById("aiChatReset");

    const messagesContainer =
        document.getElementById("aiChatMessages");

    const form =
        document.getElementById("aiChatForm");

    const input =
        document.getElementById("aiChatInput");

    const sendButton =
        document.getElementById("aiChatSend");

    const characterCount =
        document.getElementById(
            "aiChatCharacterCount"
        );

    const suggestionButtons =
        document.querySelectorAll(
            "[data-ai-prompt]"
        );

    if (!panel ||
        !messagesContainer ||
        !form ||
        !input ||
        !sendButton) {

        console.warn(
            "VDK AI Chat: Thiếu thành phần giao diện."
        );

        return;
    }

    let history = [];
    let activeController = null;
    let isSending = false;
    let typingElement = null;
    let lastQuestion = "";

    function openChat() {
        panel.hidden = false;

        requestAnimationFrame(() => {
            panel.classList.add(
                "is-open"
            );
        });

        panel.setAttribute(
            "aria-hidden",
            "false"
        );

        openButton?.setAttribute(
            "aria-expanded",
            "true"
        );

        setTimeout(() => {
            input.focus();
            scrollToBottom();
        }, 100);
    }

    function closeChat() {
        panel.classList.remove(
            "is-open",
            "open",
            "show",
            "active"
        );

        panel.setAttribute(
            "aria-hidden",
            "true"
        );

        openButton?.setAttribute(
            "aria-expanded",
            "false"
        );

        setTimeout(() => {
            if (!panel.classList.contains(
                "is-open"
            )) {
                panel.hidden = true;
            }
        }, 230);
    }

    function escapeHtml(value) {
        const element =
            document.createElement("div");

        element.textContent =
            value ?? "";

        return element.innerHTML;
    }

    function formatContent(value) {
        let content =
            escapeHtml(value ?? "");

        content = content.replace(
            /\*\*(.+?)\*\*/g,
            "<strong>$1</strong>"
        );

        content = content.replace(
            /`([^`]+)`/g,
            "<code>$1</code>"
        );

        content = content.replace(
            /\r?\n/g,
            "<br>"
        );

        return content;
    }

    function getCurrentTime() {
        return new Intl.DateTimeFormat(
            "vi-VN",
            {
                hour: "2-digit",
                minute: "2-digit"
            }
        ).format(new Date());
    }

    function scrollToBottom() {
        requestAnimationFrame(() => {
            messagesContainer.scrollTop =
                messagesContainer.scrollHeight;
        });
    }

    function createMessage(
        role,
        content,
        isError = false
    ) {
        const row =
            document.createElement("div");

        row.className =
            `vdk-ai-message ${role}`;

        row.dataset.aiGenerated =
            "true";

        if (isError) {
            row.classList.add(
                "vdk-ai-message-error"
            );
        }

        const contentWrapper =
            document.createElement("div");

        contentWrapper.className =
            "vdk-ai-message-content";

        const bubble =
            document.createElement("div");

        bubble.className =
            "vdk-ai-message-bubble";

        bubble.innerHTML =
            formatContent(content);

        const time =
            document.createElement("span");

        time.className =
            "vdk-ai-message-time";

        time.textContent =
            getCurrentTime();

        contentWrapper.appendChild(
            bubble
        );

        contentWrapper.appendChild(
            time
        );

        if (role === "assistant") {
            const avatar =
                document.createElement("div");

            avatar.className =
                "vdk-ai-message-avatar";

            avatar.innerHTML =
                '<i class="bi bi-stars"></i>';

            row.appendChild(
                avatar
            );
        }

        row.appendChild(
            contentWrapper
        );

        messagesContainer.appendChild(
            row
        );

        scrollToBottom();

        return row;
    }

    function addUserMessage(content) {
        return createMessage(
            "user",
            content
        );
    }

    function addAssistantMessage(
        content,
        isError = false
    ) {
        return createMessage(
            "assistant",
            content,
            isError
        );
    }

    function showTyping() {
        removeTyping();

        const row =
            document.createElement("div");

        row.className =
            "vdk-ai-message assistant";

        row.dataset.aiTyping =
            "true";

        const avatar =
            document.createElement("div");

        avatar.className =
            "vdk-ai-message-avatar";

        avatar.innerHTML =
            '<i class="bi bi-stars"></i>';

        const contentWrapper =
            document.createElement("div");

        contentWrapper.className =
            "vdk-ai-message-content";

        const bubble =
            document.createElement("div");

        bubble.className =
            "vdk-ai-message-bubble vdk-ai-typing-bubble";

        bubble.innerHTML = `
            <span class="vdk-ai-typing-dots">
                <span class="vdk-ai-typing-dot"></span>
                <span class="vdk-ai-typing-dot"></span>
                <span class="vdk-ai-typing-dot"></span>
            </span>

            <span class="vdk-ai-typing-status">
                Đang suy nghĩ...
            </span>
        `;

        contentWrapper.appendChild(
            bubble
        );

        row.appendChild(
            avatar
        );

        row.appendChild(
            contentWrapper
        );

        messagesContainer.appendChild(
            row
        );

        typingElement = row;

        scrollToBottom();
    }

    function removeTyping() {
        typingElement?.remove();
        typingElement = null;

        messagesContainer
            .querySelectorAll(
                "[data-ai-typing='true']"
            )
            .forEach(element => {
                element.remove();
            });
    }

    function setBusy(isBusy) {
        isSending = isBusy;

        input.disabled = isBusy;
        sendButton.disabled = isBusy;

        sendButton.classList.toggle(
            "is-loading",
            isBusy
        );

        sendButton.innerHTML =
            isBusy
                ? '<i class="bi bi-arrow-repeat"></i>'
                : '<i class="bi bi-send-fill"></i>';
    }

    function resizeInput() {
        input.style.height =
            "auto";

        input.style.height =
            `${Math.min(
                input.scrollHeight,
                110
            )}px`;
    }

    function updateCharacterCount() {
        const length =
            input.value.length;

        if (characterCount) {
            characterCount.textContent =
                `${length}/${MAX_MESSAGE_LENGTH}`;
        }

        resizeInput();
    }

    function getRecentHistory() {
        return history.slice(
            -MAX_HISTORY_MESSAGES
        );
    }

    async function readResponse(response) {
        const text =
            await response.text();

        if (!text) {
            return {};
        }

        try {
            return JSON.parse(text);
        }
        catch {
            return {
                detail: text
            };
        }
    }

    function getServerError(
        payload,
        status
    ) {
        if (status === 429) {
            return (
                "Trợ lý đang nhận quá nhiều yêu cầu. " +
                "Bạn vui lòng chờ một lát rồi thử lại."
            );
        }

        if (status === 401 ||
            status === 403) {

            return (
                "Gemini API chưa được xác thực. " +
                "Vui lòng kiểm tra API key."
            );
        }

        return (
            payload?.detail ||
            payload?.title ||
            payload?.message ||
            "Trợ lý AI đang gặp sự cố."
        );
    }

    async function callChatApi(
        message,
        recentHistory
    ) {
        activeController?.abort();

        const controller =
            new AbortController();

        activeController =
            controller;

        let timedOut =
            false;

        const timeoutId =
            window.setTimeout(() => {
                timedOut = true;
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
                            message:
                                message,

                            history:
                                recentHistory
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
                await readResponse(
                    response
                );

            if (!response.ok) {
                throw new Error(
                    getServerError(
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
                    "Máy chủ không trả về câu trả lời hợp lệ."
                );
            }

            return reply;
        }
        catch (error) {
            if (error?.name === "AbortError") {
                if (timedOut) {
                    throw new Error(
                        "Trợ lý phản hồi quá lâu. " +
                        "Bạn hãy thử lại với câu hỏi ngắn hơn."
                    );
                }

                throw new Error(
                    "Yêu cầu đã được hủy."
                );
            }

            if (error instanceof TypeError) {
                throw new Error(
                    "Không thể kết nối tới máy chủ AI."
                );
            }

            throw error;
        }
        finally {
            clearTimeout(
                timeoutId
            );

            if (activeController ===
                controller) {

                activeController = null;
            }
        }
    }

    async function sendMessage(
        customMessage = null
    ) {
        if (isSending) {
            return;
        }

        const message =
            (
                customMessage ??
                input.value
            ).trim();

        if (!message) {
            input.focus();
            return;
        }

        if (message.length >
            MAX_MESSAGE_LENGTH) {

            addAssistantMessage(
                `Câu hỏi không được vượt quá ${MAX_MESSAGE_LENGTH} ký tự.`,
                true
            );

            return;
        }

        lastQuestion =
            message;

        const previousHistory =
            getRecentHistory();

        addUserMessage(
            message
        );

        history.push({
            role:
                "user",

            content:
                message
        });

        input.value =
            "";

        updateCharacterCount();

        setBusy(
            true
        );

        showTyping();

        try {
            const reply =
                await callChatApi(
                    message,
                    previousHistory
                );

            removeTyping();

            addAssistantMessage(
                reply
            );

            history.push({
                role:
                    "assistant",

                content:
                    reply
            });

            if (history.length > 12) {
                history =
                    history.slice(-12);
            }
        }
        catch (error) {
            removeTyping();

            console.error(
                "VDK AI Error:",
                error
            );

            addAssistantMessage(
                error?.message ||
                "Có lỗi xảy ra khi kết nối với trợ lý AI.",
                true
            );
        }
        finally {
            removeTyping();

            setBusy(
                false
            );

            input.focus();
        }
    }

    function resetChat() {
        activeController?.abort();
        activeController = null;

        history = [];
        lastQuestion = "";

        removeTyping();
        setBusy(false);

        messagesContainer
            .querySelectorAll(
                "[data-ai-generated='true']"
            )
            .forEach(element => {
                element.remove();
            });

        input.value =
            "";

        updateCharacterCount();

        addAssistantMessage(
            "Cuộc trò chuyện đã được làm mới. Bạn cần mình hỗ trợ tìm sách gì?"
        );

        input.focus();
    }

    form.addEventListener(
        "submit",
        event => {
            event.preventDefault();

            void sendMessage();
        }
    );

    input.addEventListener(
        "input",
        updateCharacterCount
    );

    input.addEventListener(
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

    suggestionButtons.forEach(
        button => {
            button.addEventListener(
                "click",
                () => {
                    const prompt =
                        button.dataset.aiPrompt;

                    if (!prompt ||
                        isSending) {
                        return;
                    }

                    openChat();

                    void sendMessage(
                        prompt
                    );
                }
            );
        }
    );

    openButton?.addEventListener(
        "click",
        openChat
    );

    closeButton?.addEventListener(
        "click",
        closeChat
    );

    minimizeButton?.addEventListener(
        "click",
        closeChat
    );

    resetButton?.addEventListener(
        "click",
        resetChat
    );

    window.addEventListener(
        "beforeunload",
        () => {
            activeController?.abort();
        }
    );

    updateCharacterCount();

    console.info(
        "VDK Premium AI ChatBox đã khởi tạo."
    );
})();