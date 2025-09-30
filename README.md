# fluid_simulation
## 概要 
- 流体シミュレーション勉強用リポジトリ  
  
## Stable-Fluid 
「Jos Stam. Real-Time Fluid Dynamics for Games, 2003」を参考にほぼそのままUnityに移植して実装
論文の詳しい解説は、メモにまとめた
(https://github.com/Shimo888/Learning-Notes/issues/16)

### CPU実装
[ソースコード](https://github.com/Shimo888/fluid_simulation/blob/master/Assets/App/StableFluid/StableFluidSimulator2DCpu.cs)
- 論文のコードをほぼそのまま移植  
- 実装めっちゃ重いので使いものにならない  
- インタラクション部分の実装は省いた（初期値をノイズにしてそれっぽくした） 
- 格子の幅と高さが1という仮定の計算がテクスチャのアスペクト比などの汎用性を下げているので、どうにかする 

### GPU実装
[ソースコード(C#側)](https://github.com/Shimo888/fluid_simulation/blob/master/Assets/App/StableFluid/StableFluidSimulator2DGpu.cs)  
[ソースコード(ComputeShader側)](https://github.com/Shimo888/fluid_simulation/blob/master/Assets/App/StableFluid/Compute/StableFluid2d.compute)  
- CPUの実装をComputeShaderに移植  
- 拡散や射影ステップの反復ステップには、ヤコビ法を使用した
  - ガウスザイデル法は並列処理しづらいため 
  - とはいってもコードはほぼ同じ  
- ComputeShader側のBufferを節約するために、変数名がInputやOutputみたいな名前になり可読性が下がってしまってる(いい方法あるの？？)

## その他
- 汎用可能なシミュレーションの基盤を作成したいので、のちのち別リポジトリに移移してモジュール化したい(Assets/MySimulator)  
