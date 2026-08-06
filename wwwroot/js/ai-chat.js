(() => {
    "use strict";

    const root = document.getElementById("vdkAiChat");

    if (!root) {
        return;
    }

    const toggleButton =
        document.getElementById("aiChatToggle");

    const closeButton =
        document.getElementById("aiChatClose");

    const clearButton =
        document.getElementById("aiChatClear");

    const panel =
        document.getElementById("aiChatPanel");

    const messages =
        document.getElementById("aiChatMessages");

    const suggestions =
        document.getElementById("aiChatSuggestions");

    const form =
        document.getElementById("aiChatForm");

    const input =
        document.getElementById("aiChatInput");

    const sendButton =
        document.getElementById("aiChatSend");

    let isSending = false;

    function openChat() {
        panel.hidden = false;

        toggleButton.setAttribute(
            "aria-expanded",
            "true"
        );

        window.setTimeout(() => {
            input.focus();
            scrollToBottom();
        }, 50);
    }

    function closeChat() {
        panel.hidden = true;

        toggleButton.setAttribute(
            "aria-expanded",
            "false"
        );

        toggleButton.focus();
    }

    function scrollToBottom() {
        messages.scrollTop =
            messages.scrollHeight;
    }

    function createAvatar() {
        const avatar =
            document.createElement("div");

        avatar.className =
            "ai-chat__message-avatar";

        avatar.textContent = "AI";

        return avatar;
    }

    function appendMessage(
        content,
        sender,
        isError = false
    ) {
        const message =
            document.createElement("div");

        message.className =
            `ai-chat__message ai-chat__message--${sender}`;

        if (isError) {
            message.classList.add(
                "ai-chat__message--error"
            );
        }

        const bubble =
            document.createElement("div");

        bubble.className =
            "ai-chat__bubble";

        // Dùng textContent để tránh chèn HTML độc hại.
        bubble.textContent = content;

        if (sender === "assistant") {
            message.appendChild(createAvatar());
        }

        message.appendChild(bubble);
        messages.appendChild(message);

        scrollToBottom();

        return message;
    }

    function appendTypingIndicator() {
        const message =
            document.createElement("div");

        message.className =
            "ai-chat__message ai-chat__message--assistant";

        const bubble =
            document.createElement("div");

        bubble.className =
            "ai-chat__bubble ai-chat__typing";

        for (let index = 0; index < 3; index += 1) {
            bubble.appendChild(
                document.createElement("span")
            );
        }

        message.appendChild(createAvatar());
        message.appendChild(bubble);
        messages.appendChild(message);

        scrollToBottom();

        return message;
    }

    function setSendingState(sending) {
        isSending = sending;

        input.disabled = sending;
        sendButton.disabled = sending;

        sendButton.textContent =
            sending ? "…" : "➤";
    }

    function resizeInput() {
        input.style.height = "auto";

        input.style.height =
            `${Math.min(input.scrollHeight, 120)}px`;
    }

    async function readResponse(response) {
        const contentType =
            response.headers.get("content-type") || "";

        if (contentType.includes("application/json")) {
            return await response.json();
        }

        const text = await response.text();

        return {
            detail:
                text ||
                `Máy chủ trả về lỗi HTTP ${response.status}.`
        };
    }

    async function sendMessage(messageText) {
        const trimmedMessage =
            messageText.trim();

        if (!trimmedMessage || isSending) {
            return;
        }

        appendMessage(
            trimmedMessage,
            "user"
        );

        input.value = "";
        resizeInput();

        suggestions.hidden = true;
        setSendingState(true);

        const typingMessage =
            appendTypingIndicator();

        try {
            const response = await fetch(
                "/api/chat",
                {
                    method: "POST",
                    headers: {
                        "Content-Type":
                            "application/json; charset=utf-8",
                        "Accept":
                            "application/json"
                    },
                    body: JSON.stringify({
                        message: trimmedMessage,

                        // Backend hiện tại đã kiểm tra được
                        // với mảng history rỗng.
                        history: []
                    })
                }
            );

            const data =
                await readResponse(response);

            typingMessage.remove();

            if (!response.ok) {
                throw new Error(
                    data.detail ||
                    data.title ||
                    `Yêu cầu thất bại với mã ${response.status}.`
                );
            }

            const reply =
                data.reply ||
                data.message ||
                "AI đã xử lý yêu cầu nhưng không có nội dung trả về.";

            appendMessage(
                reply,
                "assistant"
            );
        }
        catch (error) {
            typingMessage.remove();

            const errorMessage =
                error instanceof Error
                    ? error.message
                    : "Không thể kết nối tới trợ lý AI.";

            appendMessage(
                `Xin lỗi, ${errorMessage}`,
                "assistant",
                true
            );
        }
        finally {
            setSendingState(false);
            input.focus();
        }
    }

    function resetConversation() {
        messages.innerHTML = "";

        appendMessage(
            "Cuộc trò chuyện đã được làm mới. Bạn cần mình hỗ trợ điều gì?",
            "assistant"
        );

        suggestions.hidden = false;
        input.value = "";
        resizeInput();
        input.focus();
    }

    toggleButton.addEventListener(
        "click",
        () => {
            if (panel.hidden) {
                openChat();
            }
            else {
                closeChat();
            }
        }
    );

    closeButton.addEventListener(
        "click",
        closeChat
    );

    clearButton.addEventListener(
        "click",
        resetConversation
    );

    form.addEventListener(
        "submit",
        async event => {
            event.preventDefault();

            await sendMessage(input.value);
        }
    );

    input.addEventListener(
        "input",
        resizeInput
    );

    input.addEventListener(
        "keydown",
        async event => {
            if (
                event.key === "Enter" &&
                !event.shiftKey
            ) {
                event.preventDefault();

                await sendMessage(input.value);
            }
        }
    );

    suggestions.addEventListener(
        "click",
        async event => {
            const button =
                event.target.closest(
                    "[data-ai-suggestion]"
                );

            if (!button) {
                return;
            }

            const suggestion =
                button.dataset.aiSuggestion || "";

            await sendMessage(suggestion);
        }
    );

    document.addEventListener(
        "keydown",
        event => {
            if (
                event.key === "Escape" &&
                !panel.hidden
            ) {
                closeChat();
            }
        }
    );
})();