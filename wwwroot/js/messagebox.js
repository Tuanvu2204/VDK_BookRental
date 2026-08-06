(() => {
    "use strict";

    const MESSAGEBOX_ID =
        "vdk-server-notifications";

    const ICON_TITLES = {
        success: "Thành công",
        error: "Không thể thực hiện",
        warning: "Cần lưu ý",
        info: "Thông báo",
        question: "Xác nhận"
    };

    const ICON_COLORS = {
        success: "#16a34a",
        error: "#dc2626",
        warning: "#d97706",
        info: "#2563eb",
        question: "#7c3aed"
    };

    function sweetAlertAvailable() {
        return typeof window.Swal !== "undefined";
    }

    function normalizeIcon(icon) {
        const allowedIcons = [
            "success",
            "error",
            "warning",
            "info",
            "question"
        ];

        return allowedIcons.includes(icon)
            ? icon
            : "info";
    }

    function normalizeMessage(message) {
        if (message === null ||
            message === undefined) {
            return "";
        }

        return String(message).trim();
    }

    function buildCustomClass(isToast) {
        return {
            popup: isToast
                ? "vdk-swal-popup vdk-swal-toast-popup"
                : "vdk-swal-popup",
            title: "vdk-swal-title",
            htmlContainer: "vdk-swal-content",
            confirmButton: "vdk-swal-confirm",
            cancelButton: "vdk-swal-cancel",
            timerProgressBar:
                "vdk-swal-progress"
        };
    }

    async function show(options = {}) {
        const icon =
            normalizeIcon(options.icon);

        const title =
            normalizeMessage(options.title) ||
            ICON_TITLES[icon];

        const message =
            normalizeMessage(
                options.message ??
                options.text
            );

        if (!message) {
            return {
                isConfirmed: false,
                isDismissed: true
            };
        }

        if (!sweetAlertAvailable()) {
            console.warn(
                "SweetAlert2 chưa được tải.",
                {
                    icon,
                    title,
                    message
                }
            );

            return {
                isConfirmed: false,
                isDismissed: true
            };
        }

        const isToast =
            options.toast === true;

        return await window.Swal.fire({
            icon,
            title,
            text: message,

            toast: isToast,
            position: isToast
                ? "top-end"
                : "center",

            width: isToast
                ? undefined
                : 470,

            showConfirmButton:
                options.showConfirmButton ??
                !isToast,

            showCancelButton:
                options.showCancelButton === true,

            confirmButtonText:
                options.confirmButtonText ??
                "Đã hiểu",

            cancelButtonText:
                options.cancelButtonText ??
                "Đóng",

            confirmButtonColor:
                ICON_COLORS[icon],

            buttonsStyling: false,

            allowOutsideClick:
                options.allowOutsideClick ??
                isToast,

            allowEscapeKey:
                options.allowEscapeKey ??
                true,

            timer:
                options.timer ??
                (isToast ? 4200 : undefined),

            timerProgressBar:
                options.timerProgressBar ??
                isToast,

            showCloseButton:
                options.showCloseButton ??
                !isToast,

            reverseButtons:
                true,

            focusConfirm:
                true,

            customClass:
                buildCustomClass(isToast),

            didOpen: popup => {
                if (isToast) {
                    popup.addEventListener(
                        "mouseenter",
                        window.Swal.stopTimer
                    );

                    popup.addEventListener(
                        "mouseleave",
                        window.Swal.resumeTimer
                    );
                }
            }
        });
    }

    async function success(message, title = "Thành công") {
        return await show({
            icon: "success",
            title,
            message,
            toast: true
        });
    }

    async function error(
        message,
        title = "Không thể thực hiện"
    ) {
        return await show({
            icon: "error",
            title,
            message,
            toast: false
        });
    }

    async function warning(
        message,
        title = "Cần lưu ý"
    ) {
        return await show({
            icon: "warning",
            title,
            message,
            toast: false
        });
    }

    async function info(
        message,
        title = "Thông báo"
    ) {
        return await show({
            icon: "info",
            title,
            message,
            toast: true
        });
    }

    async function confirm(options = {}) {
        const title =
            normalizeMessage(options.title) ||
            "Xác nhận thao tác";

        const message =
            normalizeMessage(
                options.message ??
                options.text
            );

        if (!message) {
            return false;
        }

        const result =
            await show({
                icon:
                    normalizeIcon(
                        options.icon ??
                        "question"
                    ),

                title,
                message,

                toast: false,
                showConfirmButton: true,
                showCancelButton: true,

                confirmButtonText:
                    options.confirmButtonText ??
                    "Xác nhận",

                cancelButtonText:
                    options.cancelButtonText ??
                    "Hủy",

                allowOutsideClick: false
            });

        return result.isConfirmed === true;
    }

    window.VDKMessage = {
        show,
        success,
        error,
        warning,
        info,
        confirm
    };

    /*
     * Thay alert("...") của trình duyệt,
     * tránh hộp thoại có dòng "localhost says".
     */
    window.alert = message => {
        void info(
            normalizeMessage(message),
            "Thông báo"
        );
    };

    async function displayServerNotifications() {
        const element =
            document.getElementById(
                MESSAGEBOX_ID
            );

        if (!element) {
            return;
        }

        let notifications;

        try {
            notifications =
                JSON.parse(
                    element.textContent || "[]"
                );
        } catch (error) {
            console.error(
                "Không thể đọc dữ liệu thông báo.",
                error
            );

            return;
        }

        if (!Array.isArray(notifications)) {
            return;
        }

        for (const notification of notifications) {
            const icon =
                normalizeIcon(
                    notification?.icon
                );

            const title =
                normalizeMessage(
                    notification?.title
                );

            const message =
                normalizeMessage(
                    notification?.message
                );

            if (!message) {
                continue;
            }

            /*
             * Thành công / thông tin:
             * toast gọn ở góc phải.
             *
             * Lỗi / cảnh báo:
             * message box giữa màn hình.
             */
            const useToast =
                icon === "success" ||
                icon === "info";

            await show({
                icon,
                title,
                message,
                toast: useToast
            });
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            () => {
                void displayServerNotifications();
            },
            {
                once: true
            }
        );
    } else {
        void displayServerNotifications();
    }
})();
