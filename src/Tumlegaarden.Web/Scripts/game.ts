// Ordjagt: skærmtastatur og visning af bogstaverne i gitteret, mens man skriver.
// Uden JavaScript skriver man blot i feltet og trykker Gæt. Ordet tjekkes altid på serveren.
// Byg med: npm run build:ts (output: wwwroot/js/game.js, som er committet).

const WORD_LENGTH = 5;
const form = document.getElementById("guess-form");
const input = document.getElementById("guess");

if (form instanceof HTMLFormElement && input instanceof HTMLInputElement) {
    const tiles = Array.from(document.querySelectorAll<HTMLElement>(".board-row.is-current .tile"));

    const clean = (value: string): string =>
        value.toLowerCase().replace(/[^a-zæøå]/g, "").slice(0, WORD_LENGTH);

    const render = (): void => {
        const value = input.value;
        tiles.forEach((tile, i) => {
            tile.textContent = (value[i] ?? "").toUpperCase();
            tile.classList.toggle("t-typing", i < value.length);
        });
    };

    const shake = (): void => {
        const row = document.querySelector(".board-row.is-current");
        row?.classList.remove("shake");
        void (row as HTMLElement | null)?.offsetWidth;   // genstart animationen
        row?.classList.add("shake");
    };

    input.addEventListener("input", () => {
        const cleaned = clean(input.value);
        if (cleaned !== input.value) input.value = cleaned;
        render();
    });

    form.addEventListener("submit", event => {
        if (input.value.length !== WORD_LENGTH) {
            event.preventDefault();
            shake();
        }
    });

    for (const button of Array.from(document.querySelectorAll<HTMLButtonElement>(".kb-key"))) {
        button.addEventListener("click", () => {
            const key = button.dataset.key ?? "";
            if (key === "enter") {
                if (input.value.length === WORD_LENGTH) form.requestSubmit();
                else shake();
            } else if (key === "back") {
                input.value = input.value.slice(0, -1);
            } else if (input.value.length < WORD_LENGTH) {
                input.value += key;
            }
            render();
            input.focus({ preventScroll: true });
        });
    }

    render();
}
