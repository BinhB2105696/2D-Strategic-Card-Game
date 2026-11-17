## 0. Core Principles

1. **Stay strictly within the requested scope.** Do not expand into audits, refactors, or general cleanup.
2. **Read as little as possible, and only what needs to change.** Do not rescan the project to "get oriented".
3. **Use fewer tool calls.** Batch independent operations into a single command. Every tool call makes the context get processed again and costs more.
4. **Stop early at any point that needs the user's action** (for example, applying changes to assets through the Unity Editor, or granting a permission). Hand off clearly, then stop. Do not keep investigating around that point.
5. **Do not invent extra work.** If the request is ambiguous, ask **one** short question in a normal message, or choose the simplest option and state the assumption in one line.
6. **Never open pop-up dialogs, question forms, or any interactive prompt that waits for user input while work is in progress.** Waiting for input during a running task wastes usage. If information is missing, finish what can be done, then ask in a normal message and stop.

---

## 1. Starting or Resuming a Session

- **DO NOT** re-read design documents, notes, or history just because it is a new day or the previous run was interrupted.
- Only read `docs/HANDOFF.md` (if it exists, at most ~60 lines) to learn the current state.
- For long documents (game rules, card design, etc.): **read only the relevant section** using `grep` or keyword search, never the whole file.
- When asked to "continue the interrupted part": run `git diff --stat` and `git diff` **only on changed files**, then continue. Do not re-read unchanged files.
- Do not summarize what was done earlier unless the user asks.

---

## 2. Reading Files — Limits

### 2.1 Folders and file types to NEVER scan, list, or read (unless the user names them explicitly)

- `Library/`, `Temp/`, `Obj/`, `Logs/`, `Builds/`, `Build/`, `UserSettings/`, `.vs/`, `.idea/`, `.git/`
- `Packages/` (third-party packages), `Assets/Plugins/`, `Assets/TextMesh Pro/`
- `*.meta` files
- Binary, image, and audio files: `*.png`, `*.jpg`, `*.psd`, `*.wav`, `*.mp3`, `*.ogg`, `*.fbx`
- Generated files: `*.csproj`, `*.sln`, `*.unitypackage`

### 2.2 Unity YAML assets (`*.asset`, `*.prefab`, `*.unity`, `*.mat`, `*.anim`, `*.controller`)

- These files are very long and expensive in tokens. **Never read a whole file.**
- Read only when truly necessary, and only a few lines using `grep -n "field_name" file` or `sed -n 'a,bp'`.
- Prefer changing card data through **scripts / Editor tools** rather than hand-editing YAML.
- Do not open many card assets at once to "compare". For bulk edits, write or reuse an Editor menu tool and tell the user to run it.

### 2.3 Code reading rules

- Use `grep -n` / `rg -n` to locate the target, then read **at most 80–120 lines** around it.
- Never `cat` a long file. If a file is over 300 lines, reading by range is mandatory.
- Do not re-read a section already read in the same session unless the file has changed.
- Limit searches to `Assets/Scripts/` (or the actual code folder of the project), never the whole project root.

---

## 3. Git

- Use compact forms and restrict paths:
  - `git status --short -- Assets/Scripts Assets/Editor docs`
  - `git diff --stat -- <path>`
- **Never** run a bare `git status` on the whole project (Unity generates a huge number of files and the output is enormous).
- **Never** run a long `git log`. If needed: `git log --oneline -n 5`.
- Do not run a full `git diff` when you only need to know which files changed.
- Do not commit, stash, reset, or switch branches unless the user asks.

---

## 4. Compiling and Testing

- **Compile at most once per group of changes**, after all edits are finished. Do not compile after every small edit.
- Only compile again if the previous run produced errors related to your own changes.
- When a command has long output (compile, tests, logs): **filter the output** and keep only relevant errors and warnings, for example lines containing `error CS`. Never print the full log.
- Test just enough for the change made (for example, a few relevant HP/KO cases). Do not expand into a comprehensive test suite unless asked.
- Do not re-run tests that already passed just to "be sure".

---

## 5. Tool Calls, Interaction, and Permissions

- Batch independent read commands into **one** call (for example, chain several `grep`/`sed` commands with `&&`, or run them in parallel).
- Target: **no more than 3–4 tool calls for a small request.** If more are expected, state the plan in one line and batch the steps.
- **Absolutely do not open pop-up dialogs, question panels, forms, or any asynchronous prompt that lets the user type input while a task is running.** Keep working or stop; never sit waiting for input mid-process. If a question is unavoidable, ask it as one short plain message after finishing the current step, then stop and wait.
- **When an action requires a permission, approval, or confirmation before it can proceed, stop immediately.** Do not retry, probe, or try workarounds. Instead, give the user clear, numbered, step-by-step instructions to grant the permission, including:
  1. What exactly is being blocked, and why it is needed.
  2. Where to click or which setting to change (exact menu path, command, or file).
  3. How to confirm it worked.
  4. What to reply with so work can resume (for example, "Done").

  Keep these instructions short and concrete, then stop and wait.
- Do not call tools again just to confirm results that are already clear.
- After writing or editing a file, do not re-read the whole file to verify. View only the diff of the changed part if needed.

---

## 6. Writing and Editing Files

- Use targeted edits (patch/replace) instead of rewriting whole files.
- Rewrite a file only when unavoidable, only for small files, and state the reason.
- Do not reformat, rename variables, reorder code, or clean up anything outside the requested scope.
- Asset-saving operations in Editor tools must be **limited to exactly the assets being changed** (avoid project-wide `SaveAssets` / `Refresh` where possible).
- Do not create new note, report, or documentation files unless requested.

---

## 7. Response Style

- Be brief. Default to a **maximum of ~10 lines** for result reports, containing:
  1. What was done (1–3 bullets, naming files).
  2. Test result (pass/fail, main error if any).
  3. What the user must do next (if anything).
- Do not paste back code that was edited unless asked. Name the file and function changed.
- No long explanations, and do not re-list work from earlier turns.
- No lengthy apologies or self-evaluation of the process unless the user asks.

---

## 8. When the User Asks About Usage / Token Consumption

- Answer honestly: if exact figures are unavailable, say so. Do not guess numbers.
- **Do not automatically halt work and start a long investigation** just because the user is worried. That behavior itself burns extra tokens.
- Reply in at most ~8 lines: name the likely factors (model and reasoning level, conversation length, large tool output) and propose one concrete action (switch model, or open a new chat with a handoff file).

---

## 9. Context Management and Handoff

- When the conversation is already long (many file reads or large logs), **proactively suggest** opening a new chat instead of continuing.
- Before suggesting it, update `docs/HANDOFF.md` (overwrite, **at most ~60 lines**) containing:
  - **Current goal**
  - **Done** (with files changed)
  - **In progress / next step**
  - **Important decisions and assumptions**
  - **Quick verification commands**
- The new chat should read only `docs/HANDOFF.md`, not every document again.

---

## 10. Model Selection Guidance (for the user; the agent does not switch models itself)

| Type of work | Suggestion |
|---|---|
| Small edits, changing values, adding a field, fixing compile errors | Lighter model, reasoning **Low** |
| Clear-scope C# changes, writing Editor tools | Mid-tier model, reasoning **Low/Medium** |
| Rules-system design, complex state bugs, bot AI | Strongest model, reasoning **High**, only when needed |

- Use **Standard** speed; avoid accelerated modes when the goal is saving usage.
- If the current task is small but is running on a heavy configuration, remind the user in one line.

---

## 11. Pre-Request Checklist (quick self-check)

- [ ] Do I really need to read this file? Could a grep of one section replace reading the whole file?
- [ ] Can independent commands be batched into one call?
- [ ] Am I expanding beyond the requested scope?
- [ ] Has the command output been filtered to be short?
- [ ] Am I about to open a pop-up or prompt that waits for input mid-task? If so, don't.
- [ ] Is a permission or confirmation needed? If so, stop and give numbered steps.
- [ ] Have I reached a point that needs the user's action in Unity? If so, hand off and stop.
