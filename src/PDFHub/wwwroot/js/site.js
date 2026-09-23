// Filter dropdowns on the list page search immediately.
document.querySelectorAll("select[data-autosubmit]").forEach(select =>
    select.addEventListener("change", () => select.form.requestSubmit()));

// Destructive forms ask first.
document.querySelectorAll("form[data-confirm]").forEach(form =>
    form.addEventListener("submit", e => {
        if (!confirm(form.dataset.confirm)) e.preventDefault();
    }));

// Close the account menu when clicking elsewhere.
document.addEventListener("click", e => {
    document.querySelectorAll("details.menu[open]").forEach(menu => {
        if (!menu.contains(e.target)) menu.open = false;
    });
});

// File drop zones show the chosen file name.
document.querySelectorAll("[data-dropzone]").forEach(zone => {
    const input = zone.querySelector("input[type=file]");
    const text = zone.querySelector("[data-dropzone-text]");
    const original = text.textContent;
    input.addEventListener("change", () => {
        const file = input.files[0];
        zone.classList.toggle("has-file", !!file);
        text.textContent = file ? `${file.name} (${(file.size / 1048576).toFixed(1)} MB)` : original;
    });
    ["dragenter", "dragover"].forEach(t => zone.addEventListener(t, () => zone.classList.add("drag")));
    ["dragleave", "drop"].forEach(t => zone.addEventListener(t, () => zone.classList.remove("drag")));
});

// PdfCode field: same rules as the old Excel macro, checked while typing; the server re-checks on save.
const codeInput = document.querySelector("[data-pdfcode]");
if (codeInput) {
    const sections = JSON.parse(codeInput.dataset.sections || "{}");
    const status = document.getElementById("code-status");
    const sectionName = document.getElementById("section-name");
    const drawingId = codeInput.dataset.id || "";
    let timer, lastChecked;

    const show = (message, ok) => {
        status.textContent = message;
        status.className = "code-status " + (ok ? "ok" : "bad");
    };

    const normalize = value => {
        let code = value.trim().toUpperCase();
        if (/^[A-Z]{2}\d{5}$/.test(code)) code = code.slice(0, 2) + "-" + code.slice(2);
        return code;
    };

    const formatError = code => {
        if (code.length !== 8 || code[2] !== "-" || !/^[A-Z]{2}/.test(code)) return "รูปแบบ PdfCode ไม่ถูกต้อง ตัวอย่าง : HI-07666";
        if (!/^\d{5}$/.test(code.slice(3))) return "ตัวเลขท้ายต้องมี 5 หลัก";
        if (!sections[code.slice(0, 2)]) return `ไม่พบประเภท ${code.slice(0, 2)} ในรายการ Section`;
        return null;
    };

    const check = async () => {
        const code = normalize(codeInput.value);
        sectionName.textContent = sections[code.slice(0, 2)] || "—";
        if (!code) { status.textContent = ""; return; }
        if (code.length < 8) { status.textContent = ""; return; }

        const error = formatError(code);
        if (error) { show(error, false); return; }
        if (code === lastChecked) return;
        lastChecked = code;

        try {
            const res = await fetch(`/api/pdfcode?code=${encodeURIComponent(code)}&id=${drawingId}`);
            if (!res.ok) return;
            const data = await res.json();
            if (normalize(codeInput.value) !== code) return;
            show(data.valid ? `✓ ใช้รหัสนี้ได้ · Section ${data.section}` : data.error, data.valid);
        } catch { /* offline: the server validates on save anyway */ }
    };

    codeInput.addEventListener("input", () => {
        const { selectionStart, selectionEnd } = codeInput;
        codeInput.value = codeInput.value.toUpperCase();
        codeInput.setSelectionRange(selectionStart, selectionEnd);
        clearTimeout(timer);
        timer = setTimeout(check, 250);
    });
    codeInput.addEventListener("blur", () => {
        codeInput.value = normalize(codeInput.value);
        check();
    });
    if (codeInput.value) check();
}
