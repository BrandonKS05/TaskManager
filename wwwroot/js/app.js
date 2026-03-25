const listNavEl = document.getElementById("list-nav");
const taskListEl = document.getElementById("task-list");
const emptyEl = document.getElementById("empty-state");
const formEl = document.getElementById("add-form");
const titleInput = document.getElementById("title-input");
const tagInput = document.getElementById("tag-input");
const dueInput = document.getElementById("due-input");
const priorityInput = document.getElementById("priority-input");
const greetEl = document.getElementById("greet-line");
const sortGridEl = document.getElementById("sort-grid");

const btnNewList = document.getElementById("btn-new-list");

let lists = [];
let selectedListId = null;
let tasksCache = [];
let sortMode = "none";

const DEVICE_ID_STORAGE_KEY = "fastodo_device_id";
const reminderNotifiedTaskIds = new Set();

function ensureDeviceId() {
  let id = localStorage.getItem(DEVICE_ID_STORAGE_KEY);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(DEVICE_ID_STORAGE_KEY, id);
  }
  return id;
}

function requestNotificationPermission() {
  if (!("Notification" in window)) return;
  if (Notification.permission === "default") {
    Notification.requestPermission().catch(() => {});
  }
}

function isDueWithinNext24Hours(dueYmd) {
  if (!dueYmd || typeof dueYmd !== "string") return false;
  const parts = dueYmd.split("-").map(Number);
  if (parts.length !== 3 || parts.some((n) => Number.isNaN(n))) return false;
  const [y, m, d] = parts;
  const start = new Date(y, m - 1, d, 0, 0, 0, 0);
  const end = new Date(y, m - 1, d, 23, 59, 59, 999);
  const now = Date.now();
  const winEnd = now + 24 * 60 * 60 * 1000;
  return start.getTime() <= winEnd && end.getTime() >= now;
}

async function runReminderCheck() {
  if (!("Notification" in window) || Notification.permission !== "granted") return;
  try {
    const res = await api("/api/lists");
    if (!res.ok) return;
    const allLists = await res.json();
    for (const list of allLists) {
      const tr = await api(`/api/lists/${list.id}/tasks`);
      if (!tr.ok) continue;
      const tasks = await tr.json();
      for (const task of tasks) {
        if (task.isComplete) continue;
        if (!task.dueDate) continue;
        if (!isDueWithinNext24Hours(task.dueDate)) continue;
        if (reminderNotifiedTaskIds.has(task.id)) continue;
        reminderNotifiedTaskIds.add(task.id);
        new Notification("Fastodo", {
          body: `Reminder: ${task.title} is due soon.`,
        });
      }
    }
  } catch (_) {}
}

function startOfDay(d) {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

function formatLocalYmd(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function formatTaskDate(iso) {
  const d = new Date(iso);
  return formatLocalYmd(d);
}

function formatDueDisplay(ymd) {
  if (!ymd || typeof ymd !== "string") return "—";
  const parts = ymd.split("-").map(Number);
  if (parts.length !== 3 || parts.some((n) => Number.isNaN(n))) return ymd;
  const [y, m, d] = parts;
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  return `${months[m - 1]} ${d}, ${y}`;
}

function starRatingEl(priority) {
  const n = Math.max(1, Math.min(5, Number(priority) || 1));
  const wrap = document.createElement("span");
  wrap.className = "task-card__stars";
  wrap.setAttribute("aria-label", `Priority ${n} out of 5`);
  for (let i = 0; i < 5; i++) {
    const s = document.createElement("span");
    s.className = "task-card__star" + (i < n ? " is-on" : "");
    s.textContent = "★";
    s.setAttribute("aria-hidden", "true");
    wrap.append(s);
  }
  return wrap;
}

function setGreeting() {
  const h = new Date().getHours();
  let g = "Good evening.";
  if (h < 12) g = "Good morning.";
  else if (h < 17) g = "Good afternoon.";
  greetEl.textContent = g;
}

function currentListName() {
  const L = lists.find((l) => l.id === selectedListId);
  return L ? L.name : "";
}

function displayTag(task) {
  const raw = task.tag && String(task.tag).trim();
  if (raw) return "#" + raw.replace(/^#/, "");
  const name = currentListName().toLowerCase().replace(/\s+/g, "");
  return name ? "#" + name : "#list";
}

async function api(path, opts = {}) {
  const { headers: extraHeaders, ...rest } = opts;
  const res = await fetch(path, {
    ...rest,
    headers: {
      "Content-Type": "application/json",
      "device-id": ensureDeviceId(),
      ...extraHeaders,
    },
  });
  return res;
}

async function loadLists() {
  const res = await api("/api/lists");
  if (!res.ok) throw new Error("lists");
  lists = await res.json();
  if (!lists.length) throw new Error("lists");
  if (!selectedListId || !lists.some((l) => l.id === selectedListId)) {
    selectedListId = lists[0].id;
  }
}

async function loadTasks() {
  if (!selectedListId) return;
  const res = await api(`/api/lists/${selectedListId}/tasks`);
  if (!res.ok) throw new Error("tasks");
  tasksCache = await res.json();
}

function sortTasks(arr) {
  const copy = [...arr];
  switch (sortMode) {
    case "date":
      copy.sort((a, b) => {
        const da = a.dueDate || "";
        const db = b.dueDate || "";
        if (da !== db) return da.localeCompare(db);
        return new Date(a.createdAtUtc) - new Date(b.createdAtUtc);
      });
      break;
    case "tag":
      copy.sort((a, b) => displayTag(a).localeCompare(displayTag(b)));
      break;
    case "name":
      copy.sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: "base" }));
      break;
    case "priority":
      copy.sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0));
      break;
    default:
      break;
  }
  return copy;
}

function filteredTasks() {
  return sortTasks(tasksCache);
}

function renderListNav() {
  listNavEl.innerHTML = "";
  for (const list of lists) {
    const li = document.createElement("li");
    li.className = "list-nav__item";

    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "list-nav__btn";
    btn.textContent = list.name;
    if (list.id === selectedListId) btn.setAttribute("aria-current", "page");
    btn.addEventListener("click", async () => {
      if (list.id === selectedListId) return;
      selectedListId = list.id;
      await refreshTasks();
      renderListNav();
    });

    if (list.id === selectedListId) {
      const pill = document.createElement("div");
      pill.className = "list-nav__pill";
      const rm = document.createElement("button");
      rm.type = "button";
      rm.className = "list-nav__remove";
      rm.setAttribute("aria-label", "Delete list");
      rm.textContent = "×";
      rm.addEventListener("click", async (e) => {
        e.stopPropagation();
        if (!confirm(`Delete list “${list.name}” and its tasks?`)) return;
        const res = await api(`/api/lists/${list.id}`, { method: "DELETE" });
        if (res.status === 400) {
          alert("Cannot delete the last list.");
          return;
        }
        if (!res.ok) {
          alert("Could not delete list.");
          return;
        }
        selectedListId = null;
        await init();
      });
      pill.append(btn, rm);
      li.append(pill);
    } else {
      li.append(btn);
    }

    listNavEl.append(li);
  }
}

function renderTasks() {
  const items = filteredTasks();
  taskListEl.innerHTML = "";

  const showEmpty = items.length === 0;
  emptyEl.hidden = !showEmpty;
  if (showEmpty) {
    emptyEl.querySelector(".empty__title").textContent = "No todos yet";
    emptyEl.querySelector(".empty__hint").textContent = "Add a todo above.";
  }

  for (const task of items) {
    const li = document.createElement("li");
    li.className = "task-card" + (task.isComplete ? " is-done" : "");

    const check = document.createElement("button");
    check.type = "button";
    check.className = "task-card__check";
    check.disabled = task.isComplete;
    check.setAttribute("aria-label", task.isComplete ? "Completed" : "Mark complete");
    if (!task.isComplete) {
      check.addEventListener("click", () => completeTask(task.id));
    }

    const body = document.createElement("div");
    body.className = "task-card__body";
    const dateEl = document.createElement("span");
    dateEl.className = "task-card__date";
    dateEl.textContent = "Added " + formatTaskDate(task.createdAtUtc);
    const dueEl = document.createElement("span");
    dueEl.className = "task-card__due";
    dueEl.textContent = "Due " + formatDueDisplay(task.dueDate);
    const titleEl = document.createElement("span");
    titleEl.className = "task-card__title";
    titleEl.textContent = task.title;
    body.append(dateEl, dueEl, titleEl, starRatingEl(task.priority));

    const tagEl = document.createElement("span");
    tagEl.className = "task-card__tag";
    tagEl.textContent = displayTag(task);

    const del = document.createElement("button");
    del.type = "button";
    del.className = "task-card__del";
    del.setAttribute("aria-label", "Delete todo");
    del.textContent = "×";
    del.addEventListener("click", () => deleteTask(task.id));

    li.append(check, body, tagEl, del);
    taskListEl.append(li);
  }
}

function bindSort() {
  sortGridEl.querySelectorAll(".sort-pill").forEach((btn) => {
    btn.addEventListener("click", () => {
      sortMode = btn.dataset.sort || "none";
      sortGridEl.querySelectorAll(".sort-pill").forEach((b) => b.classList.remove("is-active"));
      btn.classList.add("is-active");
      renderTasks();
    });
  });
}

async function completeTask(id) {
  const res = await api(`/api/tasks/${id}/complete`, { method: "PATCH" });
  if (!res.ok && res.status !== 404) {
    alert("Could not update.");
    return;
  }
  await refreshTasks();
}

async function deleteTask(id) {
  const res = await api(`/api/tasks/${id}`, { method: "DELETE" });
  if (!res.ok && res.status !== 404) {
    alert("Could not delete.");
    return;
  }
  reminderNotifiedTaskIds.delete(id);
  await refreshTasks();
}

async function refreshTasks() {
  try {
    await loadTasks();
    renderTasks();
  } catch {
    taskListEl.innerHTML = "";
    emptyEl.hidden = false;
    emptyEl.querySelector(".empty__title").textContent = "Offline";
    emptyEl.querySelector(".empty__hint").textContent = "Start the server and refresh.";
  }
}

function setDefaultDueInput() {
  dueInput.value = formatLocalYmd(startOfDay(new Date()));
}

async function init() {
  requestNotificationPermission();
  setGreeting();
  setDefaultDueInput();
  try {
    await loadLists();
    renderListNav();
    await refreshTasks();
    runReminderCheck();
  } catch {
    listNavEl.innerHTML = "";
    taskListEl.innerHTML = "";
    emptyEl.hidden = false;
    emptyEl.querySelector(".empty__title").textContent = "Could not connect";
    emptyEl.querySelector(".empty__hint").textContent = "Start the server and refresh.";
  }
}

formEl.addEventListener("submit", async (e) => {
  e.preventDefault();
  const title = titleInput.value.trim();
  if (!title || !selectedListId) return;
  const due = dueInput.value;
  if (!due) {
    dueInput.reportValidity();
    return;
  }
  const tagRaw = tagInput.value.trim();
  const priority = Number(priorityInput.value) || 3;
  const body = {
    title,
    tag: tagRaw || null,
    priority,
    dueDate: due,
  };
  const res = await api(`/api/lists/${selectedListId}/tasks`, {
    method: "POST",
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    alert(err.error || "Could not add todo.");
    return;
  }
  titleInput.value = "";
  tagInput.value = "";
  priorityInput.value = "3";
  setDefaultDueInput();
  titleInput.focus();
  await refreshTasks();
  runReminderCheck();
});

btnNewList.addEventListener("click", async () => {
  const name = prompt("New list name?");
  if (!name || !name.trim()) return;
  const res = await api("/api/lists", {
    method: "POST",
    body: JSON.stringify({ name: name.trim() }),
  });
  if (!res.ok) {
    alert("Could not create list.");
    return;
  }
  const created = await res.json();
  selectedListId = created.id;
  await init();
});

bindSort();
setInterval(runReminderCheck, 60_000);
init();
