// Progressive enhancements for the sign-in form; the form works without them.
(() => {
    const form = document.querySelector("[data-login-form]");
    if (!form) {
        return;
    }

    // Show / hide password, so it can be checked before submitting.
    const toggle = form.querySelector("[data-password-toggle]");
    const password = document.getElementById(toggle?.getAttribute("aria-controls") ?? "");
    if (toggle && password) {
        toggle.hidden = false;
        toggle.addEventListener("click", () => {
            const show = password.type === "password";
            password.type = show ? "text" : "password";
            toggle.textContent = show ? "Ukryj" : "Pokaż";
            toggle.setAttribute("aria-pressed", String(show));
            password.focus();
        });
    }

    // One submit only, with visible progress while the server checks the password.
    form.addEventListener("submit", () => {
        const submit = form.querySelector("button[type=submit]");
        if (submit) {
            submit.disabled = true;
            submit.textContent = submit.dataset.busyText ?? submit.textContent;
        }
        if (password) {
            password.type = "password";
        }
    });
})();
