# Evidence placeholders

Pasta opcional para guardar snapshots simples do estado do repositório usados na elaboração deste documento.

Sugestões:

```powershell
git branch --show-current > evidence/git-branch.txt
git log -1 --oneline > evidence/git-last-commit.txt
git status --short > evidence/git-status.txt
git ls-files > evidence/git-files.txt
```
