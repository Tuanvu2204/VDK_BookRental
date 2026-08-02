document.addEventListener(
    "DOMContentLoaded",
    function () {

        const navbar =
            document.getElementById(
                "mainNavbar"
            );

        const scrollTopButton =
            document.getElementById(
                "scrollTopButton"
            );

        const pageProgress =
            document.getElementById(
                "pageProgress"
            );

        const navbarContent =
            document.getElementById(
                "mainNavbarContent"
            );

        const homeNavLink =
            document.getElementById(
                "homeNavLink"
            );

        const guideNavLink =
            document.getElementById(
                "rentalGuideNavLink"
            );

        function updateScrollElements() {

            const scrolled =
                window.scrollY > 20;

            if (navbar) {
                navbar.classList.toggle(
                    "scrolled",
                    scrolled
                );
            }

            if (scrollTopButton) {
                scrollTopButton.classList.toggle(
                    "show",
                    window.scrollY > 350
                );
            }
        }

        updateScrollElements();

        window.addEventListener(
            "scroll",
            updateScrollElements,
            {
                passive: true
            }
        );

        if (scrollTopButton) {

            scrollTopButton.addEventListener(
                "click",
                function () {

                    window.scrollTo({
                        top: 0,
                        behavior: "smooth"
                    });
                }
            );
        }

        if (navbarContent) {

            navbarContent
                .querySelectorAll(
                    "a:not(.dropdown-toggle)"
                )
                .forEach(function (link) {

                    link.addEventListener(
                        "click",
                        function () {

                            if (window.innerWidth >= 992) {
                                return;
                            }

                            const collapse =
                                bootstrap.Collapse
                                    .getInstance(
                                        navbarContent
                                    );

                            if (collapse) {
                                collapse.hide();
                            }
                        }
                    );
                });
        }

        document.querySelectorAll(
            "a[href]"
        )
            .forEach(function (link) {

                link.addEventListener(
                    "click",
                    function (event) {

                        const href =
                            link.getAttribute("href");

                        if (!href ||
                            href.startsWith("#") ||
                            href.startsWith("mailto:") ||
                            href.startsWith("tel:") ||
                            link.target === "_blank" ||
                            event.ctrlKey ||
                            event.metaKey ||
                            event.shiftKey) {

                            return;
                        }

                        let targetUrl;

                        try {
                            targetUrl =
                                new URL(
                                    href,
                                    window.location.href
                                );
                        }
                        catch {
                            return;
                        }

                        if (targetUrl.origin !==
                            window.location.origin) {

                            return;
                        }

                        if (!pageProgress) {
                            return;
                        }

                        pageProgress.classList.add(
                            "show"
                        );

                        pageProgress.style.width =
                            "35%";

                        window.setTimeout(
                            function () {

                                pageProgress.style.width =
                                    "75%";
                            },
                            130
                        );
                    }
                );
            });

        window.addEventListener(
            "pageshow",
            function () {

                if (!pageProgress) {
                    return;
                }

                pageProgress.style.width =
                    "100%";

                window.setTimeout(
                    function () {

                        pageProgress.classList.remove(
                            "show"
                        );

                        pageProgress.style.width =
                            "0";
                    },
                    260
                );
            }
        );

        function updateHashMenu() {

            const isGuideHash =
                window.location.hash ===
                "#rentalGuide";

            if (homeNavLink) {
                homeNavLink.classList.toggle(
                    "active",
                    !isGuideHash
                );
            }

            if (guideNavLink) {
                guideNavLink.classList.toggle(
                    "active",
                    isGuideHash
                );
            }
        }

        updateHashMenu();

        window.addEventListener(
            "hashchange",
            updateHashMenu
        );
    }
);