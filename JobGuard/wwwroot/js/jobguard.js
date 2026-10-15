// JobGuard Client Interactivity: OCR extraction trigger, Cookie Banner, Modal Preferences

document.addEventListener("DOMContentLoaded", function () {
    initCookieBanner();
    initOcrUpload();
    initPasswordToggles();
    initSidebar();
    initThemeSettings();
    initMicroAnimations();
});


// Collapsible left navigation rail
function initSidebar() {
    const toggle = document.getElementById("sidebarToggle");
    if (!toggle) return;
    const key = "JobGuard_SidebarExpanded";
    const applyState = expanded => {
        document.body.classList.toggle("sidebar-expanded", expanded);
        toggle.setAttribute("aria-expanded", expanded ? "true" : "false");
        toggle.setAttribute("aria-label", expanded ? "Collapse navigation" : "Expand navigation");
        toggle.setAttribute("title", expanded ? "Collapse navigation" : "Expand navigation");
    };
    let expanded = false;
    try { expanded = localStorage.getItem(key) === "true"; } catch { /* storage may be disabled */ }
    applyState(expanded);
    toggle.addEventListener("click", () => {
        expanded = !document.body.classList.contains("sidebar-expanded");
        applyState(expanded);
        try { localStorage.setItem(key, String(expanded)); } catch { /* optional preference */ }
    });
}

// =========================================================================
// Cookie Consent Management
// =========================================================================
function initCookieBanner() {
    const banner = document.getElementById("cookieBanner");
    if (!banner) return;

    // Consent is requested once per browser tab/session, not on every route.
    // A new session prompts again as requested; navigation and refreshes do not.
    let acceptedThisSession = false;
    try { acceptedThisSession = sessionStorage.getItem("JobGuard_ConsentPromptHandled") === "true"; } catch { }
    banner.style.display = acceptedThisSession ? "none" : "block";

    const btnAccept = document.getElementById("btnAcceptCookies");
    const btnReject = document.getElementById("btnRejectCookies");
    if (btnAccept) btnAccept.addEventListener("click", () => saveCookieConsent(true, true));
    if (btnReject) btnReject.addEventListener("click", () => saveCookieConsent(false, false));
}

function openCookiePreferences() {
    const banner = document.getElementById("cookieBanner");
    if (banner) {
        banner.style.display = "block";
        banner.classList.add("cookie-banner-open");
        try { sessionStorage.removeItem("JobGuard_ConsentPromptHandled"); } catch { }
    } else {
        window.location.href = "/Legal/Cookies";
    }
}

function saveCookieConsent(analytics, preferences) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    // Immediately set a client cookie with Path=/ so the banner stays hidden right away
    setClientCookie("JobGuard_ConsentStatus", `${analytics}_${preferences}`, 365);
    try { sessionStorage.setItem("JobGuard_ConsentPromptHandled", "true"); } catch { }

    const banner = document.getElementById("cookieBanner");
    if (banner) { banner.style.display = "none"; banner.classList.remove("cookie-banner-open"); }

    fetch("/Legal/SetCookieConsent", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token || ""
        },
        body: JSON.stringify({
            essential: true,
            analytics: analytics,
            preferences: preferences
        })
    }).catch(err => {
        console.warn("Could not save consent to database:", err);
    });
}

function setClientCookie(name, value, days) {
    const d = new Date();
    d.setTime(d.getTime() + (days * 24 * 60 * 60 * 1000));
    document.cookie = `${name}=${value};path=/;expires=${d.toUTCString()};SameSite=Lax`;
}

function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return null;
}

// =========================================================================
// Client-side OCR Instant Extraction Preview
// =========================================================================
function initOcrUpload() {
    const fileInput = document.getElementById("fileUploadInput");
    const statusBox = document.getElementById("ocrStatusBox");
    const descTextarea = document.getElementById("jobDescriptionTextarea");

    if (!fileInput || !statusBox || !descTextarea) return;

    fileInput.addEventListener("change", function () {
        if (!fileInput.files || fileInput.files.length === 0) return;

        const file = fileInput.files[0];
        statusBox.style.display = "block";
        statusBox.className = "alert-box alert-info-custom";
        statusBox.innerHTML = `<span class="spinner-icon"></span> Extracting text from <strong>${escapeHtml(file.name)}</strong> via OCR / PDF parser...`;

        const formData = new FormData();
        formData.append("documentFile", file);

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        fetch("/Home/ExtractDocumentText", {
            method: "POST",
            headers: {
                "X-CSRF-TOKEN": token || ""
            },
            body: formData
        })
        .then(res => {
            if (!res.ok) {
                throw new Error(`Server returned status HTTP ${res.status}`);
            }
            return res.json();
        })
        .then(data => {
            if (data.success) {
                statusBox.className = "alert-box alert-success-custom";
                statusBox.innerHTML = `Successfully extracted text (${escapeHtml(data.source)}). You can review and adjust it below before analysis.`;
                descTextarea.value = data.text;
                descTextarea.focus();
            } else {
                statusBox.className = "alert-box alert-danger-custom";
                statusBox.innerHTML = `Text extraction notice: ${escapeHtml(data.message || "Failed to extract text.")}`;
            }
        })
        .catch(err => {
            statusBox.className = "alert-box alert-danger-custom";
            statusBox.innerHTML = `Extraction notice: ${escapeHtml(err.message || "Could not read file.")}. Please paste the description text directly.`;
            console.error(err);
        });
    });
}

// =========================================================================
// Password Visibility Toggle for Authentication Forms
// =========================================================================
function initPasswordToggles() {
    document.querySelectorAll(".pwd-toggle-btn").forEach(btn => {
        btn.addEventListener("click", function () {
            const targetId = this.getAttribute("data-target");
            const input = document.getElementById(targetId);
            if (!input) return;

            if (input.type === "password") {
                input.type = "text";
                this.setAttribute("aria-label", "Hide password");
                this.classList.add("pwd-shown");
            } else {
                input.type = "password";
                this.setAttribute("aria-label", "Show password");
                this.classList.remove("pwd-shown");
            }
        });
    });
}

function escapeHtml(text) {
    if (!text) return "";
    const div = document.createElement("div");
    div.innerText = text;
    return div.innerHTML;
}


// Cross-page theme preference
function initThemeSettings() {
    const validThemes = ["light", "dark", "ocean", "emerald", "sunset", "rose", "contrast"];
    let theme = "dark";
    try {
        const saved = localStorage.getItem("JobGuard_Theme");
        if (validThemes.includes(saved)) theme = saved;
    } catch { }
    applyTheme(theme);
    document.querySelectorAll("[data-theme-choice]").forEach(button => {
        button.classList.toggle("is-selected", button.dataset.themeChoice === theme);
        button.setAttribute("aria-pressed", button.dataset.themeChoice === theme ? "true" : "false");
        button.addEventListener("click", () => {
            const selected = button.dataset.themeChoice;
            if (!validThemes.includes(selected)) return;
            applyTheme(selected);
            try { localStorage.setItem("JobGuard_Theme", selected); } catch { }
            document.querySelectorAll("[data-theme-choice]").forEach(item => {
                const active = item.dataset.themeChoice === selected;
                item.classList.toggle("is-selected", active);
                item.setAttribute("aria-pressed", active ? "true" : "false");
            });
            const message = document.getElementById("themeSavedMessage");
            if (message) { message.textContent = "Theme updated and saved."; message.classList.remove("theme-message-pop"); void message.offsetWidth; message.classList.add("theme-message-pop"); }
        });
    });
}

function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    document.body?.setAttribute("data-theme", theme);
}

function initMicroAnimations() {
    document.querySelectorAll("button, .btn-primary-action, .btn-secondary-action, .jg-sidebar-link").forEach(element => {
        element.addEventListener("pointerdown", () => {
            if (!window.matchMedia("(prefers-reduced-motion: reduce)").matches) element.classList.add("interaction-press");
        });
        ["pointerup", "pointerleave", "blur", "pointercancel"].forEach(eventName => element.addEventListener(eventName, () => element.classList.remove("interaction-press")));
    });
}
