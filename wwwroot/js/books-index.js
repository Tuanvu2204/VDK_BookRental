document.addEventListener(
    "DOMContentLoaded",
    function () {

        const searchInput =
            document.getElementById(
                "searchInput"
            );

        const categorySelect =
            document.getElementById(
                "categorySelect"
            );

        const searchButton =
            document.getElementById(
                "searchButton"
            );

        const resetButton =
            document.getElementById(
                "resetSearchButton"
            );

        const emptyResetButton =
            document.getElementById(
                "emptyResetButton"
            );

        const searchHint =
            document.getElementById(
                "searchHint"
            );

        const noResult =
            document.getElementById(
                "noResult"
            );

        const visibleBookCount =
            document.getElementById(
                "visibleBookCount"
            );

        const bookItems =
            Array.from(
                document.querySelectorAll(
                    ".book-item"
                )
            );

        const categorySections =
            Array.from(
                document.querySelectorAll(
                    ".book-category-section"
                )
            );

        function normalizeText(value) {

            return (value || "")
                .normalize("NFD")
                .replace(
                    /[\u0300-\u036f]/g,
                    ""
                )
                .replace(/đ/g, "d")
                .replace(/Đ/g, "D")
                .toLowerCase()
                .trim();
        }

        function updateHint(
            visibleCount,
            keyword,
            category) {
            if (!searchHint) {
                return;
            }

            searchHint.classList.remove(
                "search-hint-success",
                "search-hint-error"
            );

            const isFiltering =
                keyword !== "" ||
                category !== "all";

            if (!isFiltering) {
                searchHint.textContent = "";
                return;
            }

            if (visibleCount > 0) {

                searchHint.textContent =
                    `Tìm thấy ${visibleCount} sách phù hợp.`;

                searchHint.classList.add(
                    "search-hint-success"
                );
            }
            else {

                searchHint.textContent =
                    "Không tìm thấy sách phù hợp.";

                searchHint.classList.add(
                    "search-hint-error"
                );
            }
        }

        function updateResetButton(
            keyword,
            category) {
            if (!resetButton) {
                return;
            }

            const hasFilter =
                keyword !== "" ||
                category !== "all";

            resetButton.classList.toggle(
                "d-none",
                !hasFilter
            );
        }

        function filterBooks() {

            if (!searchInput ||
                !categorySelect) {

                return;
            }

            const keyword =
                normalizeText(
                    searchInput.value
                );

            const selectedCategory =
                normalizeText(
                    categorySelect.value
                );

            let visibleCount = 0;

            bookItems.forEach(
                function (bookItem) {

                    const title =
                        normalizeText(
                            bookItem.dataset.title
                        );

                    const author =
                        normalizeText(
                            bookItem.dataset.author
                        );

                    const category =
                        normalizeText(
                            bookItem.dataset.category
                        );

                    const keywordMatched =
                        keyword === "" ||
                        title.includes(keyword) ||
                        author.includes(keyword) ||
                        category.includes(keyword);

                    const categoryMatched =
                        selectedCategory === "all" ||
                        category === selectedCategory;

                    const shouldShow =
                        keywordMatched &&
                        categoryMatched;

                    bookItem.classList.toggle(
                        "d-none",
                        !shouldShow
                    );

                    if (shouldShow) {
                        visibleCount++;
                    }
                }
            );

            categorySections.forEach(
                function (section) {

                    const visibleBooks =
                        section.querySelectorAll(
                            ".book-item:not(.d-none)"
                        );

                    section.classList.toggle(
                        "d-none",
                        visibleBooks.length === 0
                    );
                }
            );

            if (noResult) {

                noResult.classList.toggle(
                    "d-none",
                    visibleCount !== 0
                );
            }

            if (visibleBookCount) {

                visibleBookCount.textContent =
                    visibleCount.toString();
            }

            updateHint(
                visibleCount,
                keyword,
                selectedCategory
            );

            updateResetButton(
                keyword,
                selectedCategory
            );
        }

        function resetSearch() {

            if (searchInput) {
                searchInput.value = "";
            }

            if (categorySelect) {
                categorySelect.value = "all";
            }

            filterBooks();

            document.getElementById(
                "bookList"
            )?.scrollIntoView({
                behavior: "smooth",
                block: "start"
            });

            searchInput?.focus();
        }

        searchInput?.addEventListener(
            "input",
            filterBooks
        );

        searchInput?.addEventListener(
            "keydown",
            function (event) {

                if (event.key === "Enter") {

                    event.preventDefault();

                    filterBooks();

                    document.getElementById(
                        "bookList"
                    )?.scrollIntoView({
                        behavior: "smooth",
                        block: "start"
                    });
                }
            }
        );

        categorySelect?.addEventListener(
            "change",
            filterBooks
        );

        searchButton?.addEventListener(
            "click",
            function () {

                filterBooks();

                document.getElementById(
                    "bookList"
                )?.scrollIntoView({
                    behavior: "smooth",
                    block: "start"
                });
            }
        );

        resetButton?.addEventListener(
            "click",
            resetSearch
        );

        emptyResetButton?.addEventListener(
            "click",
            resetSearch
        );

        const revealItems =
            document.querySelectorAll(
                ".reveal-item"
            );

        if ("IntersectionObserver" in window) {

            const observer =
                new IntersectionObserver(
                    function (entries) {

                        entries.forEach(
                            function (entry) {

                                if (!entry.isIntersecting) {
                                    return;
                                }

                                entry.target.classList.add(
                                    "revealed"
                                );

                                observer.unobserve(
                                    entry.target
                                );
                            }
                        );
                    },
                    {
                        threshold: 0.1
                    }
                );

            revealItems.forEach(
                function (item) {

                    observer.observe(item);
                }
            );
        }
        else {

            revealItems.forEach(
                function (item) {

                    item.classList.add(
                        "revealed"
                    );
                }
            );
        }

        if (window.location.hash ===
            "#rentalGuide") {

            window.setTimeout(
                function () {

                    document.getElementById(
                        "rentalGuide"
                    )?.scrollIntoView({
                        behavior: "smooth",
                        block: "start"
                    });
                },
                200
            );
        }

        filterBooks();
    }
);