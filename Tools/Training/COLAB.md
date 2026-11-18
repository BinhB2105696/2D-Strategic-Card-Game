# Colab smoke training

1. Create the safe bundle locally with `make_colab_bundle.py`; it contains only prepared public-policy data and training code.
2. Upload the zip to Colab or Drive, copy it to `/content`, and unzip there. Do not train directly from thousands of Drive files.
3. In a CPU runtime run:

```bash
%cd /content/card_ai/training
!python -m pip install -r requirements-cpu.txt
!python train_imitation.py --dataset ../dataset --config smoke_train_config.json --output /content/card_ai_output
```

4. Download `card_ai_output` or copy it back to Drive. Keep `run_manifest.json`, `metrics.jsonl`, `best.pt`, and `last.pt` together.

The current smoke set has train/validation only. Do not report it as an independent test or as game-playing strength.
