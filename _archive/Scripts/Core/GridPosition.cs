// GridPosition.cs
// This is a simple data structure that represents a position on our game grid.
// Think of it like chess coordinates (A1, B2, etc.) but with numbers for both axes.

using UnityEngine;

namespace DogAndRobot.Core
{
    // 'System.Serializable' lets Unity show this in the Inspector
    // so you can see and edit grid positions on your GameObjects
    [System.Serializable]
    public struct GridPosition
    {
        public int x;
        public int y;

        // Constructor - a quick way to create a new GridPosition
        public GridPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        // Convert grid coordinates to actual world position
        // We multiply by cellSize to space things out properly
        public Vector3 ToWorldPosition(float cellSize = 1f)
        {
            return new Vector3(x * cellSize, y * cellSize, 0);
        }

        // Create a GridPosition from a world position (snaps to nearest grid cell)
        public static GridPosition FromWorldPosition(Vector3 worldPos, float cellSize = 1f)
        {
            return new GridPosition(
                Mathf.RoundToInt(worldPos.x / cellSize),
                Mathf.RoundToInt(worldPos.y / cellSize)
            );
        }

        // These let us do math with grid positions, like:
        // newPos = currentPos + GridPosition.Up
        public static GridPosition operator +(GridPosition a, GridPosition b)
        {
            return new GridPosition(a.x + b.x, a.y + b.y);
        }

        public static GridPosition operator -(GridPosition a, GridPosition b)
        {
            return new GridPosition(a.x - b.x, a.y - b.y);
        }

        // Check if two positions are the same
        public static bool operator ==(GridPosition a, GridPosition b)
        {
            return a.x == b.x && a.y == b.y;
        }

        public static bool operator !=(GridPosition a, GridPosition b)
        {
            return !(a == b);
        }

        // Useful preset directions - makes code more readable
        // Instead of writing "new GridPosition(0, 1)" we can write "GridPosition.Up"
        public static GridPosition Up => new GridPosition(0, 1);
        public static GridPosition Down => new GridPosition(0, -1);
        public static GridPosition Left => new GridPosition(-1, 0);
        public static GridPosition Right => new GridPosition(1, 0);
        public static GridPosition Zero => new GridPosition(0, 0);

        // Calculate distance between two grid positions (Manhattan distance)
        // This counts how many grid steps it takes to get from A to B
        // (moving only up/down/left/right, no diagonals)
        public int ManhattanDistanceTo(GridPosition other)
        {
            return Mathf.Abs(x - other.x) + Mathf.Abs(y - other.y);
        }

        // Required when overriding == operator
        public override bool Equals(object obj)
        {
            if (obj is GridPosition other)
                return this == other;
            return false;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ (y.GetHashCode() << 2);
        }

        // Makes debugging easier - you'll see "GridPosition(3, 5)" instead of gibberish
        public override string ToString()
        {
            return $"GridPosition({x}, {y})";
        }
    }
}