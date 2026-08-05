# Epic 17: Documentation & Code Quality
## Story 17.2: Implement Project Wiki

### Description
The project needs a structured and easily navigable Wiki on GitHub to allow users and contributors to quickly learn how to install, use, configure, and contribute to Promptino, without having to read a massive monolithic README.

### Acceptance Criteria
- [ ] A dedicated `docs/wiki` folder is created in the repository to store the markdown files (which will be synced to the GitHub Wiki).
- [ ] The current monolithic content from `https://github.com/lorenzoperrone/promptino/wiki` (and the README) is split into logical pages.
- [ ] A `_Sidebar.md` file is created to provide navigation across all Wiki pages.
- [ ] A `Home.md` file is created as the landing page of the Wiki.

### Proposed Wiki Structure
The developer should create the following markdown files:

1. **`Home.md`**: High-level overview of Promptino, quick feature summary, link to releases.
2. **`Installation-and-Quick-Start.md`**: Prerequisites, how to download and run, basic steps.
3. **`User-Guide.md`**: Detailed explanation of features, `[[marker:Label]]` tags, stage directions, hotkeys.
4. **`Development-and-Architecture.md`**: Project structure overview, build instructions, link to `ARCHITECTURE.md`.
5. **`_Sidebar.md`**: Markdown list containing links to all the above pages for navigation.

### Implementation Notes
- Use GitHub Flavored Markdown.
- Ensure all relative links between wiki pages work correctly in the GitHub Wiki environment.
- Any images should be referenced via absolute raw URLs from the main repo or stored in a dedicated `images` folder in the wiki repository.
