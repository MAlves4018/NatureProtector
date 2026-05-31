$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
latexmk -pdf -interaction=nonstopmode -file-line-error -synctex=1 poster.tex
Pop-Location
