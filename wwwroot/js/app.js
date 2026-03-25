const listNavEl = document.getElementById("list-nav");
const taskListEl = document.getElementById("task-list");
const emptyEl = document.getElementById("empty-state");
const formEl = document.getElementById("add-form");
const titleInput = document.getElementById("title-input");
const tagInput = document.getElementById("tag-input");
const importanceInput = document.getElementById("importance-input");
const complexityInput = document.getElementById("complexity-input");
const greetEl = document.getElementById("greet-line");
const calTitleEl = document.getElementById("cal-title");
const calDaysEl = document.getElementById("cal-days");
const sortGridEl = document.getElementById("sort-grid");

const btnNewList = document.getElementById("btn-new-list");
const btnShowAll = document.getElementById("btn-show-all");
const calPrev = document.getElementById("cal-prev");
const calNext = document.getElementById("cal-next");

let lists = [];
let selectedListId = null;
let tasksCache = [];
let sortMode = "none";
let filterDate = null;
let calView = { y: new Date().getFullYear(), m: new Date().getMonth() };

const DEVICE_ID_STORAGE_KEY = "fastodo_device_id";

function ensureDeviceId() {
  let id = localStorage.getItem(DEVICE_ID_STORAGE_KEY);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(DEVICE_ID_STORAGE_KEY, id);
  }
  return id;
}

function startOfDay(d) {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

function sameDay(a, b) {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}

function formatTaskDate(iso) {
  const d = new Date(iso);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function formatLocalYmd(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function dueDateForNewTask() {
  const d = filterDate || startOfDay(new Date());
  return formatLocalYmd(d);
}

function starRatingEl(priority) {
  const n = Math.max(1, Math.min(5, Number(priority) || 1));
  const wrap = document.createElement("span");
  wrap.className = "task-card__stars";
  wrap.setAttribute("aria-label", `Urgency ${n} out of 5`);
  for (let i = 0; i < 5; i++) {
    const s = document.createElement("span");
    s.className = "task-card__star" + (i < n ? " is-on" : "");
    s.textContent = "★";
    s.setAttribute("aria-hidden", "true");
    wrap.append(s);
  }
  return wrap;
}

function taskLocalDay(iso) {
  return startOfDay(new Date(iso));
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
      copy.sort((a, b) => new Date(a.createdAtUtc) - new Date(b.createdAtUtc));
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
  let t = tasksCache;
  if (filterDate) {
    t = t.filter((task) => sameDay(taskLocalDay(task.createdAtUtc), filterDate));
  }
  return sortTasks(t);
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
    emptyEl.querySelector(".empty__title").textContent =
      tasksCache.length === 0 ? "No todos yet" : "No todos for this view";
    emptyEl.querySelector(".empty__hint").textContent =
      tasksCache.length === 0
        ? "Add a todo above."
        : "Pick another date or tap Show All.";
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
    dateEl.textContent = formatTaskDate(task.createdAtUtc);
    const titleEl = document.createElement("span");
    titleEl.className = "task-card__title";
    titleEl.textContent = task.title;
    body.append(dateEl, titleEl, starRatingEl(task.priority));

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

function renderCalendar() {
  const { y, m } = calView;
  calTitleEl.textContent = `${y} ${["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"][m]}`;

  const first = new Date(y, m, 1);
  const startPad = first.getDay();
  const daysInMonth = new Date(y, m + 1, 0).getDate();
  const prevDays = new Date(y, m, 0).getDate();

  calDaysEl.innerHTML = "";
  const today = startOfDay(new Date());

  let cells = [];
  for (let i = 0; i < startPad; i++) {
    const dayNum = prevDays - startPad + i + 1;
    cells.push({ dayNum, muted: true, inMonth: false, d: new Date(y, m - 1, dayNum) });
  }
  for (let d = 1; d <= daysInMonth; d++) {
    cells.push({ dayNum: d, muted: false, inMonth: true, d: new Date(y, m, d) });
  }
  const rem = cells.length % 7;
  const trail = rem === 0 ? 0 : 7 - rem;
  for (let i = 1; i <= trail; i++) {
    cells.push({ dayNum: i, muted: true, inMonth: false, d: new Date(y, m + 1, i) });
  }

  for (const c of cells) {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "cal__day";
    b.textContent = String(c.d.getDate());
    if (c.muted) b.classList.add("is-muted");
    if (sameDay(c.d, today)) b.classList.add("is-today");
    if (filterDate && sameDay(c.d, filterDate)) b.classList.add("is-selected");
    if (c.muted) {
      b.disabled = true;
    } else {
      b.addEventListener("click", () => {
        filterDate = startOfDay(c.d);
        renderCalendar();
        renderTasks();
      });
    }
    calDaysEl.append(b);
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
  await refreshTasks();
}

async function refreshTasks() {
  try {
    await loadTasks();
    renderTasks();
    renderCalendar();
  } catch {
    taskListEl.innerHTML = "";
    emptyEl.hidden = false;
    emptyEl.querySelector(".empty__title").textContent = "Offline";
    emptyEl.querySelector(".empty__hint").textContent = "Start the server and refresh.";
  }
}

async function init() {
  setGreeting();
  try {
    await loadLists();
    renderListNav();
    await refreshTasks();
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
  const tagRaw = tagInput.value.trim();
  const importance = Number(importanceInput.value) || 3;
  const complexity = Number(complexityInput.value) || 3;
  const body = {
    title,
    tag: tagRaw || null,
    importance,
    complexity,
    dueDate: dueDateForNewTask(),
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
  importanceInput.value = "3";
  complexityInput.value = "3";
  titleInput.focus();
  await refreshTasks();
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

btnShowAll.addEventListener("click", () => {
  filterDate = null;
  renderCalendar();
  renderTasks();
});

calPrev.addEventListener("click", () => {
  calView.m -= 1;
  if (calView.m < 0) {
    calView.m = 11;
    calView.y -= 1;
  }
  renderCalendar();
});

calNext.addEventListener("click", () => {
  calView.m += 1;
  if (calView.m > 11) {
    calView.m = 0;
    calView.y += 1;
  }
  renderCalendar();
});

bindSort();
init();
