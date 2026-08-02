document.addEventListener(
    "DOMContentLoaded",
    function () {

        initializeDeleteForms();

        initializeBookImagePreview();

        initializeBookForm();

        initializeAutoCloseAlerts();
    }
);

function initializeDeleteForms() {

    const deleteForms =
        document.querySelectorAll(
            ".delete-book-form"
        );

    deleteForms.forEach(function (form) {

        form.addEventListener(
            "submit",
            function (event) {

                const bookTitle =
                    form.getAttribute(
                        "data-book-title"
                    ) || "sách này";

                const confirmed =
                    window.confirm(
                        "Bạn chắc chắn muốn xóa “"
                        + bookTitle
                        + "”?\n\n"
                        + "Thao tác này không thể hoàn tác."
                    );

                if (!confirmed) {

                    event.preventDefault();

                    return;
                }

                const button =
                    form.querySelector(
                        "button[type='submit']"
                    );

                if (button) {

                    button.disabled = true;

                    button.innerHTML =
                        '<span class="spinner-border '
                        + 'spinner-border-sm me-1"></span>'
                        + 'Đang xóa';
                }
            }
        );
    });
}

function initializeBookImagePreview() {

    const imageInput =
        document.getElementById(
            "ImageFile"
        );

    const preview =
        document.getElementById(
            "bookImagePreview"
        );

    if (!imageInput || !preview) {
        return;
    }

    imageInput.addEventListener(
        "change",
        function () {

            const file =
                imageInput.files &&
                imageInput.files[0];

            if (!file) {
                return;
            }

            if (!file.type.startsWith(
                "image/")) {

                imageInput.value = "";

                window.alert(
                    "Vui lòng chọn đúng tệp ảnh."
                );

                return;
            }

            const maximumSize =
                5 * 1024 * 1024;

            if (file.size > maximumSize) {

                imageInput.value = "";

                window.alert(
                    "Ảnh không được vượt quá 5 MB."
                );

                return;
            }

            const reader =
                new FileReader();

            reader.onload =
                function (event) {

                    preview.src =
                        event.target.result;
                };

            reader.readAsDataURL(
                file
            );
        }
    );
}

function initializeBookForm() {

    const form =
        document.getElementById(
            "bookForm"
        );

    if (!form) {
        return;
    }

    form.addEventListener(
        "submit",
        function () {

            if (!form.checkValidity()) {
                return;
            }

            const submitButton =
                form.querySelector(
                    "button[type='submit']"
                );

            if (!submitButton) {
                return;
            }

            submitButton.disabled = true;

            submitButton.innerHTML =
                '<span class="spinner-border '
                + 'spinner-border-sm me-2"></span>'
                + 'Đang lưu...';
        }
    );
}

function initializeAutoCloseAlerts() {

    window.setTimeout(
        function () {

            const alerts =
                document.querySelectorAll(
                    ".alert.alert-dismissible"
                );

            alerts.forEach(
                function (alertElement) {

                    if (typeof bootstrap ===
                        "undefined") {
                        return;
                    }

                    const instance =
                        bootstrap.Alert
                            .getOrCreateInstance(
                                alertElement
                            );

                    instance.close();
                }
            );
        },
        5000
    );
}