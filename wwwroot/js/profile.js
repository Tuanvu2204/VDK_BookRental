document.addEventListener(
    "DOMContentLoaded",
    function () {

        initializeAvatarUpload();

        initializeAvatarRemove();

        initializePasswordToggle();

        initializePasswordValidation();

        initializeNormalForms();

        initializeAutoCloseAlerts();
    }
);

// =====================================================
// UPLOAD AVATAR BẰNG AJAX
// =====================================================

function initializeAvatarUpload() {

    const form =
        document.getElementById(
            "avatarUploadForm"
        );

    const input =
        document.getElementById(
            "AvatarFile"
        );

    const submitButton =
        document.getElementById(
            "saveAvatarButton"
        );

    const information =
        document.getElementById(
            "avatarFileInformation"
        );

    if (!form ||
        !input ||
        !submitButton) {

        return;
    }

    let selectedFile = null;

    input.addEventListener(
        "change",
        async function () {

            clearAvatarMessage();

            const file =
                input.files &&
                input.files[0];

            selectedFile = null;

            if (!file) {

                if (information) {
                    information.textContent = "";
                }

                return;
            }

            const validation =
                validateOriginalImage(file);

            if (!validation.valid) {

                input.value = "";

                showAvatarMessage(
                    validation.message,
                    "danger"
                );

                return;
            }

            selectedFile = file;

            showOriginalPreview(file);

            if (information) {

                information.textContent =
                    `${file.name} · ` +
                    `${formatFileSize(file.size)}`;
            }
        }
    );

    form.addEventListener(
        "submit",
        async function (event) {

            event.preventDefault();

            clearAvatarMessage();

            const file =
                selectedFile ||
                (
                    input.files &&
                    input.files[0]
                );

            if (!file) {

                showAvatarMessage(
                    "Vui lòng chọn ảnh đại diện.",
                    "warning"
                );

                input.focus();

                return;
            }

            const validation =
                validateOriginalImage(file);

            if (!validation.valid) {

                showAvatarMessage(
                    validation.message,
                    "danger"
                );

                return;
            }

            setAvatarLoading(
                submitButton,
                true,
                "Đang xử lý ảnh..."
            );

            try {

                const processedFile =
                    await compressAvatarImage(
                        file,
                        1400,
                        1400,
                        0.86
                    );

                if (processedFile.size >
                    5 * 1024 * 1024) {

                    throw new Error(
                        "Ảnh sau khi nén vẫn vượt quá 5 MB."
                    );
                }

                setAvatarLoading(
                    submitButton,
                    true,
                    "Đang tải ảnh..."
                );

                const formData =
                    new FormData(form);

                formData.delete(
                    "AvatarFile"
                );

                formData.append(
                    "AvatarFile",
                    processedFile,
                    processedFile.name
                );

                const response =
                    await fetch(
                        form.action,
                        {
                            method: "POST",

                            body: formData,

                            headers: {
                                "X-Requested-With":
                                    "XMLHttpRequest"
                            },

                            credentials:
                                "same-origin"
                        }
                    );

                const result =
                    await parseJsonResponse(
                        response
                    );

                if (!response.ok ||
                    !result.success) {

                    throw new Error(
                        result.message ||
                        "Không thể cập nhật ảnh đại diện."
                    );
                }

                updateAvatarPreview(
                    result.avatarUrl
                );

                showAvatarMessage(
                    result.message ||
                    "Cập nhật ảnh đại diện thành công.",
                    "success"
                );

                input.value = "";

                selectedFile = null;

                if (information) {
                    information.textContent = "";
                }

                // Tải lại để navbar nhận AvatarUrl mới
                // từ Session.
                window.setTimeout(
                    function () {

                        window.location.reload();
                    },
                    850
                );
            }
            catch (error) {

                console.error(
                    "Avatar upload failed:",
                    error
                );

                const networkMessage =
                    error instanceof TypeError
                        ? "Không thể kết nối máy chủ. " +
                        "Kiểm tra xem dự án còn đang chạy " +
                        "trong Visual Studio hay không."
                        : error.message;

                showAvatarMessage(
                    networkMessage,
                    "danger"
                );
            }
            finally {

                setAvatarLoading(
                    submitButton,
                    false
                );
            }
        }
    );
}

function validateOriginalImage(file) {

    const allowedTypes = [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    if (!allowedTypes.includes(
        file.type)) {

        return {
            valid: false,

            message:
                "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP."
        };
    }

    const maximumOriginalSize =
        25 * 1024 * 1024;

    if (file.size >
        maximumOriginalSize) {

        return {
            valid: false,

            message:
                "Ảnh gốc không được vượt quá 25 MB."
        };
    }

    return {
        valid: true,
        message: ""
    };
}

// =====================================================
// NÉN VÀ THU NHỎ ẢNH
// =====================================================

async function compressAvatarImage(
    file,
    maximumWidth,
    maximumHeight,
    quality) {

    const image =
        await loadImageFile(file);

    const originalWidth =
        image.naturalWidth ||
        image.width;

    const originalHeight =
        image.naturalHeight ||
        image.height;

    if (!originalWidth ||
        !originalHeight) {

        throw new Error(
            "Không thể xác định kích thước ảnh."
        );
    }

    const scale =
        Math.min(
            maximumWidth /
            originalWidth,

            maximumHeight /
            originalHeight,

            1
        );

    const targetWidth =
        Math.max(
            1,
            Math.round(
                originalWidth * scale
            )
        );

    const targetHeight =
        Math.max(
            1,
            Math.round(
                originalHeight * scale
            )
        );

    const canvas =
        document.createElement(
            "canvas"
        );

    canvas.width =
        targetWidth;

    canvas.height =
        targetHeight;

    const context =
        canvas.getContext(
            "2d",
            {
                alpha: false
            }
        );

    if (!context) {

        throw new Error(
            "Trình duyệt không hỗ trợ xử lý ảnh."
        );
    }

    // Nền trắng để ảnh PNG trong suốt
    // không bị chuyển thành nền đen.
    context.fillStyle =
        "#ffffff";

    context.fillRect(
        0,
        0,
        targetWidth,
        targetHeight
    );

    context.imageSmoothingEnabled =
        true;

    context.imageSmoothingQuality =
        "high";

    context.drawImage(
        image,
        0,
        0,
        targetWidth,
        targetHeight
    );

    const blob =
        await canvasToBlob(
            canvas,
            "image/jpeg",
            quality
        );

    URL.revokeObjectURL(
        image.src
    );

    if (!blob) {

        throw new Error(
            "Không thể nén ảnh đã chọn."
        );
    }

    return new File(
        [blob],
        `avatar_${Date.now()}.jpg`,
        {
            type: "image/jpeg",
            lastModified: Date.now()
        }
    );
}

function loadImageFile(file) {

    return new Promise(
        function (resolve, reject) {

            const image =
                new Image();

            const objectUrl =
                URL.createObjectURL(
                    file
                );

            image.onload =
                function () {

                    resolve(image);
                };

            image.onerror =
                function () {

                    URL.revokeObjectURL(
                        objectUrl
                    );

                    reject(
                        new Error(
                            "Ảnh bị lỗi hoặc không thể đọc."
                        )
                    );
                };

            image.src =
                objectUrl;
        }
    );
}

function canvasToBlob(
    canvas,
    type,
    quality) {

    return new Promise(
        function (resolve) {

            canvas.toBlob(
                resolve,
                type,
                quality
            );
        }
    );
}

// =====================================================
// XEM TRƯỚC ẢNH
// =====================================================

function showOriginalPreview(file) {

    const image =
        document.getElementById(
            "profileAvatarImage"
        );

    const fallback =
        document.getElementById(
            "profileAvatarInitials"
        );

    if (!image) {
        return;
    }

    const previewUrl =
        URL.createObjectURL(file);

    image.src =
        previewUrl;

    image.classList.remove(
        "d-none"
    );

    fallback?.classList.add(
        "d-none"
    );

    image.onload =
        function () {

            URL.revokeObjectURL(
                previewUrl
            );
        };
}

function updateAvatarPreview(
    avatarUrl) {

    if (!avatarUrl) {
        return;
    }

    const cacheSafeUrl =
        avatarUrl +
        "?v=" +
        Date.now();

    const profileImage =
        document.getElementById(
            "profileAvatarImage"
        );

    const fallback =
        document.getElementById(
            "profileAvatarInitials"
        );

    if (profileImage) {

        profileImage.src =
            cacheSafeUrl;

        profileImage.classList.remove(
            "d-none"
        );
    }

    fallback?.classList.add(
        "d-none"
    );

    const navbarImage =
        document.querySelector(
            ".layout-avatar-image"
        );

    if (navbarImage) {
        navbarImage.src =
            cacheSafeUrl;
    }
}

// =====================================================
// THÔNG BÁO UPLOAD
// =====================================================

function showAvatarMessage(
    message,
    type) {

    const container =
        document.getElementById(
            "avatarUploadMessage"
        );

    if (!container) {

        window.alert(message);

        return;
    }

    const icon =
        type === "success"
            ? "bi-check-circle-fill"
            : type === "warning"
                ? "bi-exclamation-circle-fill"
                : "bi-x-circle-fill";

    container.innerHTML =
        `<div class="alert alert-${type} ` +
        `alert-dismissible fade show" role="alert">` +
        `<i class="bi ${icon} me-2"></i>` +
        `${escapeHtml(message)}` +
        `<button type="button" class="btn-close" ` +
        `data-bs-dismiss="alert" aria-label="Đóng"></button>` +
        `</div>`;
}

function clearAvatarMessage() {

    const container =
        document.getElementById(
            "avatarUploadMessage"
        );

    if (container) {
        container.innerHTML = "";
    }
}

function setAvatarLoading(
    button,
    loading,
    text) {

    if (loading) {

        if (!button.dataset.originalHtml) {

            button.dataset.originalHtml =
                button.innerHTML;
        }

        button.disabled = true;

        button.innerHTML =
            `<span class="spinner-border ` +
            `spinner-border-sm me-2"></span>` +
            `${escapeHtml(text || "Đang xử lý...")}`;

        return;
    }

    button.disabled = false;

    if (button.dataset.originalHtml) {

        button.innerHTML =
            button.dataset.originalHtml;
    }
}

async function parseJsonResponse(
    response) {

    const contentType =
        response.headers.get(
            "content-type"
        ) || "";

    if (!contentType.includes(
        "application/json")) {

        throw new Error(
            "Máy chủ trả về dữ liệu không hợp lệ. " +
            "Kiểm tra cửa sổ Output hoặc Console của Visual Studio."
        );
    }

    return await response.json();
}

// =====================================================
// XÓA AVATAR
// =====================================================

function initializeAvatarRemove() {

    const form =
        document.getElementById(
            "removeAvatarForm"
        );

    if (!form) {
        return;
    }

    form.addEventListener(
        "submit",
        function (event) {

            const confirmed =
                window.confirm(
                    "Bạn chắc chắn muốn xóa ảnh đại diện?"
                );

            if (!confirmed) {
                event.preventDefault();
            }
        }
    );
}

// =====================================================
// HIỆN / ẨN MẬT KHẨU
// =====================================================

function initializePasswordToggle() {

    document.querySelectorAll(
        "[data-password-target]"
    )
        .forEach(function (button) {

            button.addEventListener(
                "click",
                function () {

                    const targetId =
                        button.getAttribute(
                            "data-password-target"
                        );

                    const input =
                        document.getElementById(
                            targetId
                        );

                    const icon =
                        button.querySelector("i");

                    if (!input ||
                        !icon) {

                        return;
                    }

                    const showPassword =
                        input.type === "password";

                    input.type =
                        showPassword
                            ? "text"
                            : "password";

                    icon.className =
                        showPassword
                            ? "bi bi-eye-slash"
                            : "bi bi-eye";
                }
            );
        });
}

// =====================================================
// KIỂM TRA ĐỔI MẬT KHẨU
// =====================================================

function initializePasswordValidation() {

    const form =
        document.getElementById(
            "changePasswordForm"
        );

    if (!form) {
        return;
    }

    form.addEventListener(
        "submit",
        function (event) {

            const newPassword =
                document.getElementById(
                    "newPassword"
                )?.value || "";

            const confirmPassword =
                document.getElementById(
                    "confirmPassword"
                )?.value || "";

            if (newPassword !==
                confirmPassword) {

                event.preventDefault();

                window.alert(
                    "Mật khẩu xác nhận không khớp."
                );
            }
        }
    );
}

// =====================================================
// FORM THÔNG THƯỜNG
// =====================================================

function initializeNormalForms() {

    document.querySelectorAll(
        "form[data-loading-form='true']"
    )
        .forEach(function (form) {

            // Form upload đã có AJAX riêng.
            if (form.id ===
                "avatarUploadForm") {

                return;
            }

            form.addEventListener(
                "submit",
                function () {

                    if (!form.checkValidity()) {
                        return;
                    }

                    const button =
                        form.querySelector(
                            "button[type='submit']"
                        );

                    if (!button) {
                        return;
                    }

                    button.disabled = true;

                    button.innerHTML =
                        `<span class="spinner-border ` +
                        `spinner-border-sm me-2"></span>` +
                        `Đang xử lý...`;
                }
            );
        });
}

function initializeAutoCloseAlerts() {

    window.setTimeout(
        function () {

            document.querySelectorAll(
                ".alert.alert-dismissible"
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

function formatFileSize(bytes) {

    if (bytes < 1024) {
        return `${bytes} B`;
    }

    if (bytes <
        1024 * 1024) {

        return `${(
            bytes / 1024
        ).toFixed(1)} KB`;
    }

    return `${(
        bytes /
        (1024 * 1024)
    ).toFixed(2)} MB`;
}

function escapeHtml(value) {

    const element =
        document.createElement(
            "div"
        );

    element.textContent =
        value || "";

    return element.innerHTML;
}