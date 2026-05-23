import { Document, Packer, Paragraph, TextRun, HeadingLevel, AlignmentType, Table, TableRow, TableCell, WidthType, BorderStyle, ShadingType, Tab, TabStopPosition, TabStopType } from "docx";
import { writeFileSync } from "fs";

const BLUE = "2B579A";
const LIGHT_BLUE = "D6E4F0";
const DARK_GRAY = "333333";
const MEDIUM_GRAY = "666666";

function heading(text, level = HeadingLevel.HEADING_1) {
    return new Paragraph({ text, heading: level, spacing: { before: 300, after: 120 } });
}

function para(text, opts = {}) {
    return new Paragraph({
        spacing: { after: 120 },
        ...opts,
        children: [new TextRun({ text, size: 22, color: DARK_GRAY, ...opts })],
    });
}

function bold(text) {
    return new TextRun({ text, bold: true, size: 22, color: DARK_GRAY });
}

function normal(text) {
    return new TextRun({ text, size: 22, color: DARK_GRAY });
}

function richPara(children, opts = {}) {
    return new Paragraph({ spacing: { after: 120 }, children, ...opts });
}

function bullet(text, level = 0) {
    return new Paragraph({
        spacing: { after: 60 },
        bullet: { level },
        children: [new TextRun({ text, size: 22, color: DARK_GRAY })],
    });
}

function richBullet(children, level = 0) {
    return new Paragraph({
        spacing: { after: 60 },
        bullet: { level },
        children,
    });
}

function cell(text, opts = {}) {
    return new TableCell({
        children: [new Paragraph({ children: [new TextRun({ text, size: 20, color: DARK_GRAY, ...opts })] })],
        width: opts.width ? { size: opts.width, type: WidthType.PERCENTAGE } : undefined,
        shading: opts.shading ? { type: ShadingType.SOLID, color: opts.shading } : undefined,
    });
}

function headerCell(text, width) {
    return cell(text, { bold: true, color: "FFFFFF", shading: BLUE, width });
}

function tableRow(cells) {
    return new TableRow({ children: cells });
}

const doc = new Document({
    styles: {
        default: {
            heading1: {
                run: { size: 32, bold: true, color: BLUE },
            },
            heading2: {
                run: { size: 26, bold: true, color: BLUE },
            },
            heading3: {
                run: { size: 24, bold: true, color: DARK_GRAY },
            },
        },
    },
    sections: [
        {
            properties: {
                page: {
                    margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 },
                },
            },
            children: [
                // ──────────── TITLE ────────────
                new Paragraph({ spacing: { before: 600, after: 0 }, alignment: AlignmentType.CENTER, children: [] }),
                new Paragraph({
                    spacing: { before: 1200, after: 200 },
                    alignment: AlignmentType.CENTER,
                    children: [new TextRun({ text: "Custom Person Attribute Sync Engine", size: 48, bold: true, color: BLUE })],
                }),
                new Paragraph({
                    spacing: { after: 100 },
                    alignment: AlignmentType.CENTER,
                    children: [new TextRun({ text: "Rock RMS Plugin", size: 28, color: MEDIUM_GRAY })],
                }),
                new Paragraph({
                    spacing: { after: 600 },
                    alignment: AlignmentType.CENTER,
                    children: [new TextRun({ text: "by Razayya", size: 24, color: MEDIUM_GRAY, italics: true })],
                }),
                new Paragraph({
                    alignment: AlignmentType.CENTER,
                    spacing: { after: 1200 },
                    children: [new TextRun({ text: "Feature Guide & Documentation", size: 24, color: MEDIUM_GRAY })],
                }),

                // ──────────── OVERVIEW ────────────
                heading("Overview"),
                para(
                    "The Custom Person Attribute Sync Engine is a Rock RMS plugin that automatically evaluates your congregation against configurable rules and writes the results to Person Attributes. Rather than manually updating attributes or writing custom code for each business rule, you define your logic once and the engine keeps every person's attributes up to date."
                ),
                para(
                    "Whether you need to flag people who have attended a certain number of times, identify group members, filter by demographic criteria, check Data View inclusion, or evaluate multi-step completion milestones, the Sync Engine handles it with a single, unified framework."
                ),

                // ──────────── HOW IT WORKS ────────────
                heading("How It Works"),
                para(
                    "The plugin uses a three-tier organizational model that mirrors the way churches typically think about their engagement or growth processes:"
                ),
                richBullet([bold("Calculation Group"), normal(" \u2014 The top-level container. Defines the base population by filtering on Record Status, Connection Status, Campus, and/or a Data View. A group might represent \"New Member Journey\" or \"Volunteer Readiness\".")]),
                richBullet([bold("Calculation Sub-Group"), normal(" \u2014 A stage or step within a group. Sub-groups run in order and can optionally require that a person passed one or more prerequisite sub-groups before being evaluated. This lets you model funnels like \"Step 1 \u2192 Step 2 \u2192 Step 3\" or branching paths with multiple prerequisites.")]),
                richBullet([bold("Calculation"), normal(" \u2014 A single rule within a sub-group. Each calculation specifies a Calculation Type (the logic), a target Person Attribute (where the result is written), a Lava template for formatting the result, and a no-match behavior.")]),

                heading("Processing Flow", HeadingLevel.HEADING_3),
                para(
                    "When the engine runs, it processes each active Calculation Group in order:"
                ),
                bullet("1. Determine the base population by applying the group\u2019s Record Status, Connection Status, Campus, and Data View filters."),
                bullet("2. For each Sub-Group (in order), narrow the population further based on prerequisite requirements and optional Data View filters."),
                bullet("3. For each Calculation in the sub-group, invoke the Calculation Type component to evaluate every person in the working population."),
                bullet("4. Render the result through the configured Lava template and write the value to the target Person Attribute."),
                bullet("5. Record the run results (population count, matched, updated, skipped, errors) in the Run History."),

                // ──────────── CALCULATION TYPES ────────────
                heading("Calculation Types"),
                para("The engine ships with five built-in calculation types. Each type evaluates a person and returns structured merge fields that can be used in Lava templates."),

                heading("Attendance", HeadingLevel.HEADING_3),
                para("Evaluates whether a person has attended a specified group type a minimum number of times within a configurable date range (rolling window in days)."),
                new Table({
                    width: { size: 100, type: WidthType.PERCENTAGE },
                    rows: [
                        tableRow([headerCell("Merge Field", 30), headerCell("Type", 15), headerCell("Description", 55)]),
                        tableRow([cell("Matched"), cell("Boolean"), cell("True if attendance criteria were met")]),
                        tableRow([cell("AttendanceCount"), cell("Integer"), cell("Number of attendances in the date range")]),
                        tableRow([cell("LastAttendanceDate"), cell("DateTime"), cell("Most recent attendance date")]),
                    ],
                }),

                heading("Group Membership", HeadingLevel.HEADING_3),
                para("Checks whether a person is a member of a specified group type, optionally filtered by group role and active status. Returns detailed membership information including all matching groups."),
                new Table({
                    width: { size: 100, type: WidthType.PERCENTAGE },
                    rows: [
                        tableRow([headerCell("Merge Field", 30), headerCell("Type", 15), headerCell("Description", 55)]),
                        tableRow([cell("Matched"), cell("Boolean"), cell("True if person is a member")]),
                        tableRow([cell("GroupName"), cell("String"), cell("Name of the earliest-joined group")]),
                        tableRow([cell("GroupRole"), cell("String"), cell("Role in the earliest-joined group")]),
                        tableRow([cell("JoinDate"), cell("DateTime"), cell("Earliest group membership creation date")]),
                        tableRow([cell("GroupCount"), cell("Integer"), cell("Total number of matching groups")]),
                        tableRow([cell("Groups"), cell("Array"), cell("All memberships. Each entry has GroupName, GroupRole, JoinDate. Use: {% for g in Groups %}{{ g.GroupName }}{% endfor %}")]),
                    ],
                }),

                heading("Data View Inclusion", HeadingLevel.HEADING_3),
                para("Checks whether a person is included in a specified Rock Data View. This is the simplest calculation type and is useful for leveraging existing Data Views you\u2019ve already built in Rock."),
                new Table({
                    width: { size: 100, type: WidthType.PERCENTAGE },
                    rows: [
                        tableRow([headerCell("Merge Field", 30), headerCell("Type", 15), headerCell("Description", 55)]),
                        tableRow([cell("Matched"), cell("Boolean"), cell("True if person is in the Data View")]),
                        tableRow([cell("IsInDataView"), cell("Boolean"), cell("True if person is in the Data View")]),
                    ],
                }),

                heading("Person Filter", HeadingLevel.HEADING_3),
                para("Evaluates person properties and attributes against configurable filter conditions. Supports both AND (all conditions must match) and OR (any condition can match) logic. Can filter on Person properties (e.g., BirthDate, Gender, Email) as well as Person Attribute values."),
                new Table({
                    width: { size: 100, type: WidthType.PERCENTAGE },
                    rows: [
                        tableRow([headerCell("Merge Field", 30), headerCell("Type", 15), headerCell("Description", 55)]),
                        tableRow([cell("Matched"), cell("Boolean"), cell("True if filter conditions were satisfied")]),
                    ],
                }),

                heading("Completion", HeadingLevel.HEADING_3),
                para("A meta-calculation that checks whether a person has completed other calculations within the same group. You define criteria referencing sibling calculations and their expected values. This is ideal for modeling multi-step growth paths where a person must complete several steps to reach a milestone."),
                new Table({
                    width: { size: 100, type: WidthType.PERCENTAGE },
                    rows: [
                        tableRow([headerCell("Merge Field", 30), headerCell("Type", 15), headerCell("Description", 55)]),
                        tableRow([cell("Matched"), cell("Boolean"), cell("True if all required criteria are met")]),
                        tableRow([cell("CompletedCount"), cell("Integer"), cell("Number of required criteria that were met")]),
                        tableRow([cell("RequiredCount"), cell("Integer"), cell("Total number of required criteria")]),
                    ],
                }),

                // ──────────── LAVA TEMPLATES ────────────
                heading("Lava Templates & No-Match Behavior"),
                para("Each calculation uses Lava templates to format the value written to the target attribute. The merge fields listed above are available in the template. For example:"),
                para("{{ AttendanceCount }} attendances since {{ LastAttendanceDate | Date:'MMM d, yyyy' }}", { italics: true, font: "Consolas", size: 20 }),
                para("You also configure what happens when a person does not match the calculation criteria:"),
                richBullet([bold("Leave Unchanged"), normal(" \u2014 The existing attribute value is preserved (default).")]),
                richBullet([bold("Clear Value"), normal(" \u2014 The attribute value is blanked out.")]),
                richBullet([bold("Write Lava"), normal(" \u2014 A separate Lava template is rendered and written as the value.")]),

                // ──────────── POPULATION FILTERING ────────────
                heading("Population Filtering"),
                para("The engine provides multiple layers of population filtering to ensure calculations only evaluate the people who matter:"),
                richBullet([bold("Group Level"), normal(" \u2014 Record Status, Connection Status, Campus, and Data View. These are the broadest filters and define who is even considered.")]),
                richBullet([bold("Sub-Group Level"), normal(" \u2014 Optional prerequisite sub-groups (people must have passed those sub-groups\u2019 Completion calculations) and an optional additional Data View for further narrowing.")]),
                para("This layered approach means the engine avoids evaluating thousands of people who clearly don\u2019t apply, improving both performance and accuracy."),

                // ──────────── PREREQUISITE SUB-GROUPS ────────────
                heading("Prerequisite Sub-Groups"),
                para("Sub-groups can declare one or more prerequisite sub-groups. When prerequisites are configured, a person must have passed the Completion calculation in all prerequisite sub-groups to be included in the current sub-group\u2019s working population."),
                para("This supports both linear funnels (Step 1 \u2192 Step 2 \u2192 Step 3) and branching paths (Step 3 requires both Step 1 AND Step 2). You select prerequisites using a multi-select checkbox list in the sub-group editor."),

                // ──────────── AUTOMATED PROCESSING ────────────
                heading("Automated Nightly Processing"),
                para("The plugin registers a Rock Service Job that runs nightly at 2:00 AM by default. The job processes all active Calculation Groups in order and writes computed values to Person Attributes. The job schedule can be changed through Rock\u2019s standard job management interface."),
                para("The job includes a Debug Logging option that, when enabled, outputs detailed processing information to the job results for troubleshooting."),

                // ──────────── WORKFLOW INTEGRATION ────────────
                heading("Workflow Integration"),
                para("The plugin includes a Workflow Action Component that allows you to trigger the Sync Engine from any Rock Workflow. This is useful for real-time updates \u2014 for example, re-evaluating a person\u2019s attributes immediately after they complete a form or join a group, rather than waiting for the nightly job."),
                para("The workflow action takes two inputs:"),
                richBullet([bold("Person"), normal(" \u2014 The person to evaluate (uses Rock\u2019s standard Person field type).")]),
                richBullet([bold("Calculation Group"), normal(" \u2014 The group to process (uses a custom Calculation Group field type provided by the plugin).")]),
                para("The action processes all calculations in the specified group for the single person and logs the results to the workflow activity log."),

                // ──────────── RUN HISTORY ────────────
                heading("Run History & Monitoring"),
                para("Every calculation execution \u2014 whether triggered by the nightly job, a manual run, or a workflow \u2014 is recorded in the Run History. The Run History page provides:"),
                bullet("Filterable grid by calculation, date range, and success/failure status"),
                bullet("Per-run statistics: population count, matched, updated, skipped, and error counts"),
                bullet("Identification of who triggered each run (person name or \"Job\" for automated runs)"),
                bullet("Start and completion timestamps for performance monitoring"),
                bullet("A Retry button on failed runs to re-process the calculation immediately"),

                // ──────────── IMPORT/EXPORT ────────────
                heading("Import, Export & Copy"),
                para("Calculation Groups and Sub-Groups can be exported to portable JSON and imported into other Rock instances. This is useful for:"),
                bullet("Sharing configurations between environments (e.g., staging to production)"),
                bullet("Distributing standard calculation templates to other churches"),
                bullet("Backing up complex configurations before making changes"),
                para("The export format uses names and GUIDs rather than internal IDs, making it fully portable across Rock instances. Prerequisite sub-group references are exported by name and re-resolved on import."),
                para("You can also copy a Calculation Group within the same instance, which duplicates the entire configuration (all sub-groups, calculations, and their settings) with a \" (Copy)\" suffix on the name."),

                // ──────────── BATCH WRITES ────────────
                heading("Performance & Reliability"),
                para("The engine is designed for reliability at scale:"),
                richBullet([bold("Batch Writes"), normal(" \u2014 Attribute values are written in batches of 200 per database context, reducing memory usage and preventing timeouts on large populations.")]),
                richBullet([bold("Batch Reads"), normal(" \u2014 Existing attribute values are read in a single query before processing, eliminating per-person database round trips.")]),
                richBullet([bold("Partial Failure Resilience"), normal(" \u2014 If a write batch fails, the error is logged and processing continues with the remaining batches. This ensures that a single database error doesn\u2019t prevent the rest of the population from being updated.")]),
                richBullet([bold("Data View Error Handling"), normal(" \u2014 If a Data View filter fails to execute, the sub-group is safely skipped rather than proceeding with an unfiltered population.")]),
                richBullet([bold("Run History Tracking"), normal(" \u2014 Every execution is recorded with detailed statistics, making it easy to identify and diagnose issues.")]),

                // ──────────── PREVIEW ────────────
                heading("Preview & Testing"),
                para("Before running a calculation against your entire population, you can use the Preview feature to see what the engine would do. The preview shows:"),
                bullet("The total population count that would be evaluated"),
                bullet("A sample of matched and unmatched people with their computed values"),
                bullet("The rendered Lava output for each sample person"),
                para("This lets you verify your Lava templates and calculation settings are correct before writing any data."),

                // ──────────── SECURITY ────────────
                heading("Security"),
                para("Calculation Groups are securable Rock entities that support standard View, Edit, and Administrate security actions. This means you can control who can see, modify, and manage calculation configurations using Rock\u2019s built-in security model with roles and users."),

                // ──────────── GETTING STARTED ────────────
                heading("Getting Started"),
                para("After installing the plugin, navigate to the Attribute Sync Engine page under Installed Plugins. From there:"),
                bullet("1. Create a Calculation Group and configure the base population filters."),
                bullet("2. Add one or more Sub-Groups representing the stages of your process."),
                bullet("3. Within each Sub-Group, add Calculations specifying the type, target attribute, Lava template, and no-match behavior."),
                bullet("4. Use Preview to verify your configuration."),
                bullet("5. Run the calculation manually or wait for the nightly job."),
                bullet("6. Optionally, set up a Workflow to trigger real-time syncs on specific events."),
                para("The Run History page will show you the results of every execution, helping you monitor and troubleshoot your configurations over time."),
            ],
        },
    ],
});

const buffer = await Packer.toBuffer(doc);
writeFileSync("CustomPersonAttributeSyncEngine.docx", buffer);
console.log("Document generated: docs/CustomPersonAttributeSyncEngine.docx");
