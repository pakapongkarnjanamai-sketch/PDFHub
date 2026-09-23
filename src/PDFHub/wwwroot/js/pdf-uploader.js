// Bulk PDF upload: sends files one request each (three at a time) so hundreds of drawings
// don't become one huge request, and shows the result per file as it goes.
(() => {
    const form = document.getElementById("pdf-uploader");
    if (!form) return;

    const url = form.action;
    const token = form.querySelector("input[name=__RequestVerificationToken]").value;
    const overwrite = form.querySelector("[data-overwrite]");
    const panel = form.querySelector("[data-status]");
    const bar = form.querySelector("[data-bar]");
    const log = form.querySelector("[data-log]");
    const onlyProblems = form.querySelector("[data-only-problems]");
    const counts = Object.fromEntries([...form.querySelectorAll("[data-count]")].map(el => [el.dataset.count, el]));
    const labels = { ok: "สำเร็จ", skip: "ข้าม", error: "ผิดพลาด" };
    const maxBytes = Number(form.dataset.maxMb) * 1048576;

    let queue = [], total = 0, done = 0, running = 0;
    const tally = { ok: 0, skip: 0, error: 0 };

    onlyProblems.addEventListener("change", () => log.classList.toggle("only-problems", onlyProblems.checked));

    const render = () => {
        for (const k of ["ok", "skip", "error"]) counts[k].textContent = tally[k];
        counts.left.textContent = total - done;
        bar.style.width = total ? `${(done / total) * 100}%` : "0";
    };

    const addRow = (name, status, message) => {
        const tr = document.createElement("tr");
        tr.className = `row-${status}`;
        const file = document.createElement("td");
        file.className = "mono";
        file.textContent = name;
        const result = document.createElement("td");
        result.className = `st-${status}`;
        result.textContent = `${labels[status]} · ${message}`;
        tr.append(file, result);
        log.prepend(tr);
        tally[status]++;
        done++;
        render();
    };

    const upload = async file => {
        if (file.size > maxBytes) return addRow(file.name, "error", `ไฟล์ใหญ่เกิน ${form.dataset.maxMb} MB`);
        const body = new FormData();
        body.append("file", file);
        body.append("overwrite", overwrite.checked);
        body.append("__RequestVerificationToken", token);
        try {
            const res = await fetch(url, { method: "POST", body });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            addRow(file.name, data.status, data.message);
        } catch (e) {
            addRow(file.name, "error", `ส่งไฟล์ไม่สำเร็จ (${e.message})`);
        }
    };

    const pump = () => {
        while (running < 3 && queue.length) {
            running++;
            upload(queue.shift()).finally(() => { running--; pump(); });
        }
    };

    form.querySelectorAll("[data-pick]").forEach(input => input.addEventListener("change", () => {
        const pdfs = [...input.files].filter(f => /\.pdf$/i.test(f.name));
        input.value = "";
        if (!pdfs.length) return alert("ไม่พบไฟล์ .pdf ในที่เลือก");
        panel.hidden = false;
        queue.push(...pdfs);
        total += pdfs.length;
        render();
        pump();
    }));

    window.addEventListener("beforeunload", e => {
        if (done < total) e.preventDefault();
    });
})();
