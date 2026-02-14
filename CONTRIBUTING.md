# Contributing

Thanks for contributing to uStyle-Bert-VITS2.

## Development Setup

1. Clone the repository.
2. Open the project with Unity `6000.3.6f1` or later.
3. Install dependencies listed in `Packages/manifest.json`.
4. Place required model/dictionary files under `Assets/StreamingAssets/uStyleBertVITS2/`.

## Branch and PR Flow

1. Create a feature branch from `main`.
2. Keep changes focused (one topic per PR).
3. Update docs when behavior or configuration changes.
4. Open a pull request using the PR template.

## Testing

- Run relevant Unity EditMode/PlayMode tests before opening a PR.
- If tests require local model assets, note that in the PR description.
- For script changes in `scripts/`, ensure CLI help and related docs stay consistent.

## Coding Guidelines

- Follow existing naming and project structure.
- Prefer small, reviewable commits.
- Avoid unrelated formatting-only edits.

## Reporting Issues

Use GitHub Issues for bug reports and feature requests:

- https://github.com/ayutaz/uStyle-Bert-VITS2/issues
