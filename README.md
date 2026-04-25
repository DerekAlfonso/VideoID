# VideoID

GPU-accelerated video face identification for Windows, written in C#.

VideoID scans video files, detects faces using a CUDA-backed neural network, clusters similar faces into unique identities, and lets you name each person through a WPF UI. Discovered names are written back to the video files as metadata tags (ID3 / MP4 atoms).

---

## Architecture

```
Video Files
    │
    ▼  (N parallel threads — configurable)
Frame Extractor  ──────────────────────────────────────────────── Channel<VideoFrame>
    │  (configurable frame-skip, e.g. every 30th frame)
    ▼  (M GPU worker threads — configurable)
GpuFaceDetector  ──── ResNet-SSD DNN (CUDA backend, falls back to CPU)
    │
    ▼
FaceEmbeddingExtractor  ──── OpenFace DNN (128-d L2-normalised embeddings)
    │
    ▼  (single-threaded for DB consistency)
FaceClusterer  ──── incremental centroid matching (cosine distance)
    │
    ├──► FaceDatabase (LiteDB — persists across sessions)
    ├──► VideoTagWriter (TagLib# — writes Performers/Comment/MP4 atoms)
    └──► WPF UI live update
```

### Key Files

| File | Responsibility |
|------|---------------|
| `VideoID.Core/Processing/GpuFaceDetector.cs` | ResNet-SSD face detection with CUDA backend |
| `VideoID.Core/Processing/FaceEmbeddingExtractor.cs` | OpenFace 128-d embeddings, GPU or CPU |
| `VideoID.Core/Processing/VideoFrameExtractor.cs` | Frame extraction with configurable skip |
| `VideoID.Core/Processing/FaceClusterer.cs` | Thread-safe incremental identity clustering |
| `VideoID.Core/Processing/VideoProcessingPipeline.cs` | `Channel<T>`-based multi-stage pipeline |
| `VideoID.Core/Storage/FaceDatabase.cs` | LiteDB persistence for face identities |
| `VideoID.Core/Storage/VideoTagWriter.cs` | TagLib# metadata write-back |
| `VideoID.UI/ViewModels/MainViewModel.cs` | WPF MVVM orchestrator |

---

## Requirements

- Windows 10/11 x64
- .NET 8 SDK
- NVIDIA GPU with CUDA 11.x+ (optional — falls back to CPU)
- CUDA Toolkit + cuDNN installed and on PATH

---

## Setup

### 1 — Download model files

```powershell
.\scripts\Download-Models.ps1
```

Or manually place in `models/` (see [`models/DOWNLOAD_MODELS.md`](models/DOWNLOAD_MODELS.md)):

| File | ~Size | Source |
|------|-------|--------|
| `deploy_face.prototxt` | 3 KB | OpenCV samples |
| `res10_300x300_ssd.caffemodel` | 10 MB | OpenCV 3rd-party |
| `openface_nn4.small2.v1.t7` | 31 MB | CMU OpenFace |

### 2 — Build and run

```powershell
dotnet build -c Release VideoID.sln
dotnet run --project VideoID.UI -c Release
```

---

## Configuration

All settings are available in the **Settings** tab of the UI at runtime:

| Setting | Default | Description |
|---------|---------|-------------|
| Frame skip | 30 | Process every Nth frame (1 = every frame) |
| Parallel videos | CPU/2 | Concurrent video readers |
| GPU workers | 2 | Parallel DNN face-detection threads |
| Detection scale | 1.0 | Scale frame before detection (0.1–1.0, lower = faster) |
| Detection confidence | 0.65 | Minimum SSD confidence to accept a face |
| Face match threshold | 0.40 | Cosine distance to merge into existing identity |
| Use GPU | true | CUDA backend; auto-falls back to CPU |
| Write tags | true | Write names back to video metadata |

---

## Usage

1. Launch VideoID
2. Go to **Settings** → add your video directories → click **Apply**
3. Click **▶ Start Scan** on the toolbar
4. Watch unique faces appear in real-time on the **Faces** tab
5. Click **"Name this face"** on any card to assign a name
6. Named faces are written to video files automatically (can be disabled in Settings)
7. Progress and per-file status visible on the **Videos** tab
8. Click **Export** to save a TSV list of named identities

---

## Tag Format

| Container | Tag field | Content |
|-----------|-----------|---------|
| All | `Performers` / `Artists` | Semicolon-separated names |
| All | `Comment` | `Faces: Alice, Bob, …` |
| MP4/M4V | iTunes freeform atom | `com.videoid / faces` |

---

## License

MIT
