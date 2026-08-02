document.addEventListener(
    "DOMContentLoaded",
    function () {

        initializeFilterSelections();

        initializeRoleForms();

        initializeLockForms();

        initializeAvatarModal();

        initializeAlertTimeout();
    }
);

// =====================================================
// KHÔI PHỤC GIÁ TRỊ BỘ LỌC
// =====================================================

function initializeFilterSelections() {

    document.querySelectorAll(
        "select[data-selected]"
    )
        .forEach(function (select) {

            const selectedValue =
                select.dataset.selected || "";

            select.value =
                selectedValue;
        });
}

// =====================================================
// CẬP NHẬT QUYỀN
// =====================================================

function initializeRoleForms() {

    document.querySelectorAll(
        ".admin-role-form"
    )
        .forEach(function (form) {

            const select =
                form.querySelector(
                    ".admin-role-select"
                );

            const submitButton =
                form.querySelector(
                    ".admin-save-role-button"
                );

            if (!select ||
                !submitButton) {

                return;
            }

            const currentRole =
                select.dataset.currentRole || "";

            function updateButtonState() {

                submitButton.disabled =
                    select.value === currentRole;
            }

            updateButtonState();

            select.addEventListener(
                "change",
                updateButtonState
            );

            form.addEventListener(
                "submit",
                function (event) {

                    if (select.value ===
                        currentRole) {

                        event.preventDefault();

                        return;
                    }

                    const userRow =
                        form.closest("tr");

                    const userName =
                        userRow
                            ?.querySelector(
                                ".admin-user-full-name"
                            )
                            ?.textContent
                            ?.trim()
                        || "tài khoản này";

                    const confirmed =
                        window.confirm(
                            `Đổi quyền của ${userName} ` +
                            `thành ${select.value}?`
                        );

                    if (!confirmed) {

                        event.preventDefault();

                        return;
                    }

                    submitButton.disabled =
                        true;

                    submitButton.innerHTML =
                        '<span class="spinner-border ' +
                        'spinner-border-sm"></span>' +
                        'Đang lưu...';
                }
            );
        });
}

// =====================================================
// KHÓA / MỞ KHÓA
// =====================================================

function initializeLockForms() {

    document.querySelectorAll(
        ".admin-lock-form"
    )
        .forEach(function (form) {

            form.addEventListener(
                "submit",
                function (event) {

                    const userName =
                        form.dataset.userName ||
                        "tài khoản này";

                    const isLocked =
                        form.dataset.isLocked ===
                        "true";

                    const actionText =
                        isLocked
                            ? "mở khóa"
                            : "khóa";

                    const confirmed =
                        window.confirm(
                            `Bạn chắc chắn muốn ${actionText} ` +
                            `tài khoản ${userName}?`
                        );

                    if (!confirmed) {

                        event.preventDefault();

                        return;
                    }

                    const button =
                        form.querySelector(
                            "button[type='submit']"
                        );

                    if (!button) {
                        return;
                    }

                    button.disabled =
                        true;

                    button.innerHTML =
                        '<span class="spinner-border ' +
                        'spinner-border-sm"></span>' +
                        'Đang xử lý...';
                }
            );
        });
}

// =====================================================
// MODAL XEM ẢNH
// =====================================================

function initializeAvatarModal() {

    const modalElement =
        document.getElementById(
            "adminAvatarModal"
        );

    const modalImage =
        document.getElementById(
            "adminAvatarModalImage"
        );

    const modalEmpty =
        document.getElementById(
            "adminAvatarModalEmpty"
        );

    const modalTitle =
        document.getElementById(
            "adminAvatarModalLabel"
        );

    if (!modalElement ||
        !modalImage ||
        !modalEmpty ||
        !modalTitle ||
        typeof bootstrap ===
        "undefined") {

        return;
    }

    const modal =
        bootstrap.Modal.getOrCreateInstance(
            modalElement
        );

    document.querySelectorAll(
        ".admin-user-avatar-button"
    )
        .forEach(function (button) {

            button.addEventListener(
                "click",
                function () {

                    const avatarUrl =
                        button.dataset.avatarUrl ||
                        "";

                    const userName =
                        button.dataset.userName ||
                        "Người dùng";

                    modalTitle.textContent =
                        userName;

                    if (avatarUrl) {

                        modalImage.src =
                            avatarUrl +
                            (
                                avatarUrl.includes("?")
                                    ? "&"
                                    : "?"
                            ) +
                            "preview=" +
                            Date.now();

                        modalImage.alt =
                            `Ảnh đại diện của ${userName}`;

                        modalImage.classList.remove(
                            "d-none"
                        );

                        modalEmpty.classList.add(
                            "d-none"
                        );
                    }
                    else {

                        modalImage.src =
                            "";

                        modalImage.classList.add(
                            "d-none"
                        );

                        modalEmpty.classList.remove(
                            "d-none"
                        );
                    }

                    modal.show();
                }
            );
        });

    modalImage.addEventListener(
        "error",
        function () {

            modalImage.classList.add(
                "d-none"
            );

            modalEmpty.classList.remove(
                "d-none"
            );
        }
    );

    modalElement.addEventListener(
        "hidden.bs.modal",
        function () {

            modalImage.src =
                "";

            modalImage.classList.remove(
                "d-none"
            );

            modalEmpty.classList.add(
                "d-none"
            );
        }
    );
}

// =====================================================
// TỰ ĐÓNG THÔNG BÁO
// =====================================================

function initializeAlertTimeout() {

    window.setTimeout(
        function () {

            document.querySelectorAll(
                ".admin-alert"
            )
                .forEach(function (element) {

                    if (typeof bootstrap ===
                        "undefined") {

                        return;
                    }

                    bootstrap.Alert
                        .getOrCreateInstance(
                            element
                        )
                        .close();
                });

        },
        6000
    );
}