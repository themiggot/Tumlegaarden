// Små forbedringer af formularer. Uden JavaScript virker felterne stadig som almindelige dato-/tidsfelter.
// Byg med: npm run build:ts (output: wwwroot/js/forms.js, som er committet).

type PickerInput = HTMLInputElement & { showPicker?: () => void };

// Klik hvor som helst i et dato- eller tidsfelt åbner browserens kalender/ur,
// så man kan vælge datoen i stedet for at taste den.
for (const input of Array.from(document.querySelectorAll<PickerInput>('input[type="date"], input[type="time"]'))) {
    input.addEventListener("click", () => {
        if (input.readOnly || input.disabled || typeof input.showPicker !== "function") return;
        try {
            input.showPicker();
        } catch {
            // Fx hvis browseren kræver en direkte brugerhandling – så kan man stadig taste eller bruge ikonet.
        }
    });
}

// data-min-from="id": feltet kan ikke være før det andet felts dato (fx slutdato ≥ startdato),
// og kalenderen åbner derfor i den rigtige måned.
for (const input of Array.from(document.querySelectorAll<HTMLInputElement>("input[data-min-from]"))) {
    const source = document.getElementById(input.dataset.minFrom ?? "");
    if (!(source instanceof HTMLInputElement)) continue;
    const fallbackMin = input.min;
    const sync = (): void => {
        input.min = source.value || fallbackMin;
        if (input.value && source.value && input.value < source.value) input.value = source.value;
    };
    source.addEventListener("change", sync);
    sync();
}
