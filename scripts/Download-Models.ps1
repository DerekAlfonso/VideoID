#Requires -Version 5.1
<#
.SYNOPSIS
    Downloads the neural network model files required by VideoID.
.DESCRIPTION
    Places the three model files in the models/ directory relative to the repo root.
#>

$ErrorActionPreference = 'Stop'
$ModelsDir = Join-Path $PSScriptRoot '..\models'
New-Item -ItemType Directory -Force -Path $ModelsDir | Out-Null

$files = @(
    @{
        Name = 'deploy_face.prototxt'
        Url  = 'https://raw.githubusercontent.com/opencv/opencv/master/samples/dnn/face_detector/deploy.prototxt'
    },
    @{
        Name = 'res10_300x300_ssd.caffemodel'
        Url  = 'https://github.com/opencv/opencv_3rdparty/raw/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel'
    },
    @{
        Name = 'openface_nn4.small2.v1.t7'
        Url  = 'https://storage.cmusatyalab.org/openface-models/nn4.small2.v1.t7'
    }
)

foreach ($f in $files) {
    $dest = Join-Path $ModelsDir $f.Name
    if (Test-Path $dest) {
        Write-Host "  [skip] $($f.Name) already exists" -ForegroundColor Gray
        continue
    }
    Write-Host "  [download] $($f.Name) ..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $f.Url -OutFile $dest -UseBasicParsing
    Write-Host "  [ok] $($f.Name)" -ForegroundColor Green
}

Write-Host "`nAll model files are ready in: $ModelsDir" -ForegroundColor Green
