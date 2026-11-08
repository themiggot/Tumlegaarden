// Chatboksen nederst til højre. Uden JavaScript viser boksen blot et link til /Chat.
// Al tekst sættes med textContent – aldrig innerHTML – så beskeder ikke kan indsætte HTML.
// Byg med: npm run build:ts (output: wwwroot/js/chat.js, som er committet).

interface Room { id: number; name: string; isGlobal: boolean; latestId: number; }
interface Message { id: number; user: string; text: string; time: string; mine: boolean; }

const OPEN_POLL_MS = 4000;
const CLOSED_POLL_MS = 30000;
const STORE_KEY = "tumle-chat";

interface Stored { open: boolean; room: number; seen: Record<number, number>; }

function load(): Stored {
    try {
        const raw = localStorage.getItem(STORE_KEY);
        if (raw) return { open: false, room: 0, seen: {}, ...JSON.parse(raw) as Partial<Stored> };
    } catch { /* privat vindue o.l. */ }
    return { open: false, room: 0, seen: {} };
}

function save(state: Stored): void {
    try { localStorage.setItem(STORE_KEY, JSON.stringify(state)); } catch { /* ignoreres */ }
}

function el<K extends keyof HTMLElementTagNameMap>(tag: K, className?: string, text?: string): HTMLElementTagNameMap[K] {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
}

class SessionEnded extends Error { }

async function getJson<T>(url: string): Promise<T> {
    const response = await fetch(url, { headers: { Accept: "application/json" }, credentials: "same-origin" });
    if (response.redirected || !(response.headers.get("content-type") ?? "").includes("json")) throw new SessionEnded();
    if (!response.ok) throw new Error(String(response.status));
    return await response.json() as T;
}

function init(root: HTMLElement): void {
    const box = root.querySelector<HTMLDetailsElement>(".chat-box");
    const body = root.querySelector<HTMLElement>(".chat-body");
    const badge = root.querySelector<HTMLElement>(".chat-badge");
    const tokenInput = root.querySelector<HTMLInputElement>("input[name=__RequestVerificationToken]");
    if (!box || !body || !badge || !tokenInput) return;

    const endpoint = root.dataset.endpoint ?? "/Chat";
    const token = tokenInput.value;
    const state = load();
    let rooms: Room[] = [];
    let lastId = 0;
    let timer: number | undefined;
    let stopped = false;

    // --- Brugerflade ---
    const bar = el("div", "chat-bar");
    const roomLabel = el("label", "sr-only", "Rum");
    roomLabel.htmlFor = "chat-room";
    const select = el("select");
    select.id = "chat-room";
    const fullLink = el("a", "chat-full", "Fuld side");
    fullLink.href = endpoint;
    bar.append(roomLabel, select, fullLink);

    const list = el("ol", "chat-list");
    list.setAttribute("aria-live", "polite");
    list.setAttribute("aria-label", "Beskeder");

    const form = el("form", "chat-form");
    const textLabel = el("label", "sr-only", "Besked");
    textLabel.htmlFor = "chat-text";
    const text = el("textarea");
    text.id = "chat-text";
    text.rows = 2;
    text.maxLength = 1000;
    text.placeholder = "Skriv en besked … (Enter sender)";
    const send = el("button", "btn btn-primary btn-small", "Send");
    send.type = "submit";
    form.append(textLabel, text, send);

    const status = el("p", "chat-status");
    status.setAttribute("role", "status");

    body.replaceChildren(bar, list, form, status);

    // --- Hjælpere ---
    const setStatus = (message: string): void => { status.textContent = message; };

    const nearBottom = (): boolean => list.scrollHeight - list.scrollTop - list.clientHeight < 60;

    const renderMessage = (m: Message): HTMLLIElement => {
        const li = el("li", m.mine ? "chat-msg is-mine" : "chat-msg");
        li.dataset.id = String(m.id);
        const meta = el("span", "chat-meta");
        meta.append(el("strong", undefined, m.user), " ", el("span", "chat-time", m.time));
        li.append(meta, el("span", "chat-text", m.text));
        return li;
    };

    const append = (messages: Message[]): void => {
        if (messages.length === 0) return;
        const stick = nearBottom() || lastId === 0;
        list.querySelector(".chat-empty")?.remove();
        for (const m of messages) {
            if (m.id <= lastId || list.querySelector(`[data-id="${m.id}"]`)) continue;
            list.append(renderMessage(m));
            lastId = Math.max(lastId, m.id);
        }
        if (stick) list.scrollTop = list.scrollHeight;
        markSeen();
    };

    const markSeen = (): void => {
        if (!box.open || !state.room) return;
        state.seen[state.room] = Math.max(state.seen[state.room] ?? 0, lastId);
        save(state);
        updateBadge();
    };

    const updateBadge = (): void => {
        const unread = rooms.filter(r => r.latestId > (state.seen[r.id] ?? 0) && !(box.open && r.id === state.room));
        badge.hidden = unread.length === 0;
        badge.textContent = unread.length > 0 ? String(unread.length) : "";
        badge.title = unread.length > 0 ? `Nye beskeder i ${unread.map(r => r.name).join(", ")}` : "";
        for (const option of Array.from(select.options)) {
            const room = rooms.find(r => String(r.id) === option.value);
            if (room) option.textContent = (unread.includes(room) ? "• " : "") + room.name;
        }
    };

    const endSession = (): void => {
        stopped = true;
        window.clearTimeout(timer);
        form.hidden = true;
        setStatus("Du er logget ud. Genindlæs siden.");
    };

    // --- Server ---
    const loadRooms = async (): Promise<void> => {
        const data = await getJson<{ rooms: Room[] }>(`${endpoint}?handler=Rooms`);
        const firstLoad = rooms.length === 0;
        rooms = data.rooms;
        // Første gang: gammel historik tæller ikke som ulæst.
        if (firstLoad && Object.keys(state.seen).length === 0) {
            for (const r of rooms) state.seen[r.id] = r.latestId;
            save(state);
        }
        if (!rooms.some(r => r.id === state.room)) {
            // Rummet er slettet, eller adgangen er fjernet: skift til første rum og ryd listen.
            state.room = rooms[0]?.id ?? 0;
            save(state);
            lastId = 0;
            list.replaceChildren();
        }
        if (firstLoad || select.options.length !== rooms.length) {
            select.replaceChildren(...rooms.map(r => { const o = el("option", undefined, r.name); o.value = String(r.id); return o; }));
        }
        select.value = String(state.room);
        updateBadge();
    };

    const loadMessages = async (): Promise<void> => {
        if (!state.room) return;
        const room = state.room;
        const data = await getJson<{ messages: Message[] }>(`${endpoint}?handler=Messages&room=${room}&after=${lastId}`);
        if (room !== state.room) return;   // rummet er skiftet imens
        if (lastId === 0 && data.messages.length === 0 && !list.firstChild) {
            list.append(el("li", "chat-empty", "Ingen beskeder de sidste 24 timer. Skriv den første."));
        }
        append(data.messages);
    };

    const switchRoom = async (roomId: number): Promise<void> => {
        state.room = roomId;
        save(state);
        lastId = 0;
        list.replaceChildren();
        await loadMessages().catch(handleError);
        updateBadge();
    };

    const handleError = (error: unknown): void => {
        if (error instanceof SessionEnded) endSession();
        else setStatus("Kunne ikke hente chatten. Prøver igen …");
    };

    const poll = async (): Promise<void> => {
        if (stopped) return;
        try {
            if (!document.hidden) {
                await loadRooms();
                if (box.open) await loadMessages();
                if (status.textContent?.startsWith("Kunne ikke")) setStatus("");
            }
        } catch (error) {
            handleError(error);
        }
        if (!stopped) timer = window.setTimeout(poll, box.open ? OPEN_POLL_MS : CLOSED_POLL_MS);
    };

    const restartPolling = (): void => {
        window.clearTimeout(timer);
        void poll();
    };

    // --- Hændelser ---
    box.addEventListener("toggle", () => {
        state.open = box.open;
        save(state);
        if (box.open) {
            text.focus();
            list.scrollTop = list.scrollHeight;
        }
        restartPolling();
    });

    select.addEventListener("change", () => { void switchRoom(Number(select.value)); });

    text.addEventListener("keydown", event => {
        if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            form.requestSubmit();
        }
    });

    root.addEventListener("keydown", event => {
        if (event.key === "Escape" && box.open) {
            box.open = false;
            (box.querySelector("summary") as HTMLElement | null)?.focus();
        }
    });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        const value = text.value.trim();
        if (!value || !state.room) return;
        send.disabled = true;
        try {
            const response = await fetch(`${endpoint}?handler=Send`, {
                method: "POST",
                credentials: "same-origin",
                headers: { Accept: "application/json", "Content-Type": "application/x-www-form-urlencoded" },
                body: new URLSearchParams({ room: String(state.room), text: value, __RequestVerificationToken: token })
            });
            if (response.status === 429) { setStatus("For mange beskeder. Vent et øjeblik."); return; }
            if (response.redirected || !(response.headers.get("content-type") ?? "").includes("json")) { endSession(); return; }
            const data = await response.json() as { message?: Message; error?: string };
            if (!response.ok || !data.message) { setStatus(data.error ?? "Beskeden kunne ikke sendes."); return; }
            text.value = "";
            setStatus("");
            list.scrollTop = list.scrollHeight;
            append([data.message]);
        } catch {
            setStatus("Beskeden kunne ikke sendes. Prøv igen.");
        } finally {
            send.disabled = false;
            text.focus();
        }
    });

    document.addEventListener("visibilitychange", () => { if (!document.hidden) restartPolling(); });

    // --- Start ---
    root.classList.add("is-enhanced");
    box.open = state.open;
    void (async () => {
        try {
            await loadRooms();
            await loadMessages();
        } catch (error) {
            handleError(error);
        }
        if (!stopped) timer = window.setTimeout(poll, box.open ? OPEN_POLL_MS : CLOSED_POLL_MS);
    })();
}

const chatRoot = document.getElementById("chat");
if (chatRoot) init(chatRoot);
