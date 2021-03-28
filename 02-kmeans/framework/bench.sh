# DATA=../data/debug-1k
# DATA=../data/02-1M
DATA=../data/03-4M
ITERS=10
CENTROIDS=128
CORES=$1

echo "Parallel: " && srun make && srun -p small-hp -c $CORES -n 1 ./k-means $DATA $CENTROIDS $ITERS ./centroids ./asignments
echo "Serial: " && srun -p small-hp -c 32 -n 1 ../serial/k-means_serial $DATA $CENTROIDS $ITERS ./centroids ./asignments