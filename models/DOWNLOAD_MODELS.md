# Required Model Files

VideoID requires three pre-trained neural network model files placed in this `models/` directory.

## Face Detection Model (ResNet-SSD)

Used by `GpuFaceDetector` for detecting face bounding boxes.

```
models/deploy_face.prototxt
models/res10_300x300_ssd.caffemodel
```

Download from OpenCV's GitHub:
```
curl -L -o models/deploy_face.prototxt \
  "https://raw.githubusercontent.com/opencv/opencv/master/samples/dnn/face_detector/deploy.prototxt"

curl -L -o models/res10_300x300_ssd.caffemodel \
  "https://github.com/opencv/opencv_3rdparty/raw/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel"
```

## Face Embedding Model (OpenFace)

Used by `FaceEmbeddingExtractor` to produce 128-dimensional face embeddings for identity matching.

```
models/openface_nn4.small2.v1.t7
```

Download from OpenFace:
```
curl -L -o models/openface_nn4.small2.v1.t7 \
  "https://storage.cmusatyalab.org/openface-models/nn4.small2.v1.t7"
```

## Convenience Script (PowerShell)

Run from the repo root:

```powershell
.\scripts\Download-Models.ps1
```

## File Summary

| File | Size | Purpose |
|------|------|---------|
| `deploy_face.prototxt` | ~3 KB | SSD face detector architecture |
| `res10_300x300_ssd.caffemodel` | ~10 MB | SSD face detector weights |
| `openface_nn4.small2.v1.t7` | ~31 MB | Face embedding network |
