#ifndef KMEANS_IMPLEMENTATION_HPP
#define KMEANS_IMPLEMENTATION_HPP

#include <interface.hpp>
#include <exception.hpp>
#include <math.h>
#include <tbb/parallel_for.h>
#include <tbb/parallel_reduce.h>
#include <tbb/blocked_range.h>
#include <tbb/concurrent_vector.h>
#include <iostream>

template<typename POINT = point_t, typename ASGN = std::uint8_t, bool DEBUG = false>
class KMeans : public IKMeans<POINT, ASGN, DEBUG>
{
private:
	typedef typename POINT::coord_t coord_t;

	std::vector<tbb::concurrent_vector<POINT>> temp_assignments;
	size_t POINTS_SIZE;


	static coord_t distance(const POINT &point, const POINT &centroid)
	{
		std::int64_t dx = (std::int64_t)point.x - (std::int64_t)centroid.x;
		std::int64_t dy = (std::int64_t)point.y - (std::int64_t)centroid.y;
		return (coord_t)(dx*dx + dy*dy);
	}

	static std::size_t getNearestCluster(const POINT &point, const std::vector<POINT> &centroids)
	{
		coord_t minDist = distance(point, centroids[0]);
		std::size_t nearest = 0;
		for (std::size_t i = 1; i < centroids.size(); ++i) {
			coord_t dist = distance(point, centroids[i]);
			if (dist < minDist) {
				minDist = dist;
				nearest = i;
			}
		}

		return nearest;
	}


public:
	/*
	 * \brief Perform the initialization of the functor (e.g., allocate memory buffers).
	 * \param points Number of points being clustered.
	 * \param k Number of clusters.
	 * \param iters Number of refining iterations.
	 */
	virtual void init(std::size_t points, std::size_t k, std::size_t iters)
	{
		POINTS_SIZE = points;
		while (k--) {
			temp_assignments.push_back(tbb::concurrent_vector<POINT> {});
		}
	}


	/*
	 * \brief Perform the clustering and return the cluster centroids and point assignment
	 *		yielded by the last iteration.
	 * \note First k points are taken as initial centroids for first iteration.
	 * \param points Vector with input points.
	 * \param k Number of clusters.
	 * \param iters Number of refining iterations.
	 * \param centroids Vector where the final cluster centroids should be stored.
	 * \param assignments Vector where the final assignment of the points should be stored.
	 *		The indices should correspond to point indices in 'points' vector.
	 */
	virtual void compute(const std::vector<POINT> &points, std::size_t k, std::size_t iters,
		std::vector<POINT> &centroids, std::vector<ASGN> &assignments)
	{
		// Prepare for the first iteration
		centroids.resize(k);
		assignments.resize(points.size());
		for (std::size_t i = 0; i < k; ++i) {
			centroids[i] = points[i];
		}

		// Run the k-means refinements
		while (iters > 0) {
			--iters;

			// Prepare empty tmp fields.
			for (std::size_t i = 0; i < k; ++i) {
				temp_assignments[i].clear();
			}
			
			tbb::parallel_for(
				tbb::blocked_range<size_t>(size_t(0), size_t(POINTS_SIZE)), 
				[&](const tbb::blocked_range<size_t>range) {
					for (size_t i = range.begin(); i != range.end(); ++i) {
						std::size_t nearest = getNearestCluster(points[i], centroids);

						// Final loop, store in the results
						if (iters == 0) assignments[i] = (ASGN)nearest;
						temp_assignments[(ASGN)nearest].push_back(points[i]);
					}
			});

			tbb::parallel_for(
				tbb::blocked_range<size_t>(size_t(0), size_t(k)), 
				[&](const tbb::blocked_range<size_t>range) {
					for (std::size_t i = range.begin(); i < range.end(); ++i) {
						auto cluster_size = temp_assignments[i].size();
						if (cluster_size == 0) continue;	

						POINT result;
						result.x = 0;
						result.y = 0;
						result = tbb::parallel_reduce(tbb::blocked_range<size_t>(0, cluster_size), result,
						[&](const tbb::blocked_range<size_t> r, POINT p) {
							for (std::size_t j = r.begin(); j < r.end(); ++j) {
								p.x += temp_assignments[i][j].x; 
								p.y += temp_assignments[i][j].y; 
							}
							return p;
						}, [](POINT f, POINT s)->POINT {
							f.x += s.x;
							f.y += s.y;
							return f;
						});

						centroids[i].x = result.x / (std::int64_t)cluster_size;
						centroids[i].y = result.y / (std::int64_t)cluster_size;
					}
				});
		}
	}
};

#endif
