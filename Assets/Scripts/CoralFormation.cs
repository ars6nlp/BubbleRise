using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Коралловая постройка — фигура из блоков (большой квадрат, ромб, крест, арка, стена
    /// с проёмом, лестница...). Опускается вместе с миром, компактные фигуры медленно вращаются.
    ///
    /// Увернуться от неё нельзя: пузырёк зафиксирован по центру. Зато каждый блок
    /// сбивается стражем с одного удара — постройку надо начинать пробивать заранее.
    /// Дошедший до пузырька блок отнимает жизнь и разлетается.
    /// </summary>
    public class CoralFormation : MonoBehaviour
    {
        public class Block
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public bool Alive;
            public float DyingTimer;
            public float Size;
            public Vector2 Center => Tr.position;
        }

        public bool Pooled { get; set; }
        public float BlockRadius { get; private set; }
        public int AliveCount { get; private set; }
        public IReadOnlyList<Block> Blocks => _blocks;

        readonly List<Block> _blocks = new List<Block>();
        int _usedCount;
        float _spin;
        float _halfHeight;

        public void Activate(string[] shape, float cellSize, float blockRadius,
                             Vector2 position, float spin)
        {
            Pooled = false;
            BlockRadius = blockRadius;
            _spin = spin;

            int rows = shape.Length;
            int cols = 0;
            for (int r = 0; r < rows; r++) cols = Mathf.Max(cols, shape[r].Length);

            _halfHeight = rows * cellSize * 0.5f;

            transform.position = position;
            transform.rotation = Quaternion.identity;

            _usedCount = 0;
            AliveCount = 0;

            for (int r = 0; r < rows; r++)
            {
                string row = shape[r];
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] != 'X') continue;

                    // отростки рисуем только там, где сверху ничего не стоит
                    bool openAbove = r == 0 || c >= shape[r - 1].Length || shape[r - 1][c] != 'X';
                    int variant = openAbove
                        ? (Random.value < 0.55f ? 1 : 0)
                        : (Random.value < 0.30f ? 2 : 0);

                    var block = RentBlock();
                    block.Alive = true;
                    block.DyingTimer = 0f;
                    block.Size = cellSize;

                    block.Sr.sprite = SpriteFactory.CoralTile(variant);
                    // лёгкий разброс тона, чтобы кладка не выглядела штампованной
                    float tint = Random.Range(0.86f, 1.06f);
                    block.Sr.color = new Color(tint, tint * 0.98f, tint * 0.96f, 1f);
                    block.Sr.enabled = true;

                    float x = (c - (cols - 1) * 0.5f) * cellSize;
                    float y = -(r - (rows - 1) * 0.5f) * cellSize;
                    block.Tr.localPosition = new Vector3(x, y, 0f);
                    block.Tr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f));
                    block.Tr.localScale = Vector3.one * cellSize;
                    block.Tr.gameObject.SetActive(true);

                    AliveCount++;
                }
            }

            // лишние блоки из прошлой активации прячем
            for (int i = _usedCount; i < _blocks.Count; i++)
            {
                _blocks[i].Alive = false;
                _blocks[i].Tr.gameObject.SetActive(false);
            }

            gameObject.SetActive(true);
        }

        Block RentBlock()
        {
            if (_usedCount < _blocks.Count) return _blocks[_usedCount++];

            var go = new GameObject("CoralBlock");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = SortingOrders.Formation;

            var block = new Block { Tr = go.transform, Sr = sr };
            _blocks.Add(block);
            _usedCount++;
            return block;
        }

        /// <summary>Разбить блок: он перестаёт сталкиваться и осыпается.</summary>
        public void Shatter(Block block)
        {
            if (!block.Alive) return;
            block.Alive = false;
            block.DyingTimer = 0.25f;
            AliveCount--;
        }

        /// <summary>Осыпать всю постройку — например, когда всплывает босс.</summary>
        public void ShatterAll()
        {
            for (int i = 0; i < _usedCount; i++)
                if (_blocks[i].Alive) Shatter(_blocks[i]);
        }

        public bool Tick(float dt, float scrollSpeed, Playfield field)
        {
            var p = transform.position;
            p.y -= scrollSpeed * dt;
            transform.position = p;

            if (Mathf.Abs(_spin) > 0.01f)
                transform.Rotate(0f, 0f, _spin * dt);

            bool anyVisible = false;
            for (int i = 0; i < _usedCount; i++)
            {
                var b = _blocks[i];
                if (b.Alive) { anyVisible = true; continue; }
                if (b.DyingTimer <= 0f) continue;

                b.DyingTimer -= dt;
                float t = Mathf.Clamp01(b.DyingTimer / 0.25f);
                b.Tr.localScale = Vector3.one * (b.Size * (0.4f + 0.6f * t));
                var col = b.Sr.color;
                col.a = t;
                b.Sr.color = col;

                if (b.DyingTimer <= 0f) b.Tr.gameObject.SetActive(false);
                else anyVisible = true;
            }

            if (!anyVisible) return false;
            return p.y > field.Bottom - _halfHeight - 1f;
        }

        public void Deactivate()
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Alive = false;
                _blocks[i].DyingTimer = 0f;
                _blocks[i].Tr.gameObject.SetActive(false);
            }
            AliveCount = 0;
            gameObject.SetActive(false);
        }
    }
}
