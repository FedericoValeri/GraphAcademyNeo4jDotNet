﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo4j.Driver;
using Neoflix.Example;
using Neoflix.Exceptions;

namespace Neoflix.Services
{
    public class MovieService
    {
        private readonly IDriver _driver;

        /// <summary>
        /// Initializes a new instance of <see cref="MovieService"/> that handles movie database calls.
        /// </summary>
        /// <param name="driver">Instance of Neo4j Driver, which will be used to interact with Neo4j</param>
        public MovieService(IDriver driver)
        {
            _driver = driver;
        }

        /// <summary>
        /// Get a paginated list of Movies. <br/><br/>
        /// Records should be ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::all[]
        public async Task<Dictionary<string, object>[]> AllAsync(string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {

            // Open a new session.
            await using var session = _driver.AsyncSession();

            // Execute a query in a new Read Transaction.
            return await session.ExecuteReadAsync(async tx =>
            {
                
                var favorites = await GetUserFavoritesAsync(tx, userId);

                // Query Cypher:
                // 1. MATCH (m:Movie) = trova tutti i nodi che hanno l'etichetta Movie
                // 2. WHERE m.{sort} IS NOT NULL = filtra i nodi trovati in precedenza, assicurandosi che la proprietà specificata da sort non sia nulla
                // 3. RETURN m { .*, favorite: m.tmdbId IN $favorites } AS movie = ritorna i nodi Movie come un oggetto JSON, con tutte le proprietà del nodo (.* significa tutte le proprietà) e aggiunge la proprietà booleana 'favorite' basata sulla presenza di 'tmdbId' nella lista '$favorites'.
                // 4. ORDER BY m.{sort} {order.ToString("G").ToUpper()} = ordina i risultati in base alla proprietà specificata da sort e nell'ordine specificato da order (ascendente o discendente). order.ToString("G").ToUpper() converte il valore di order in una stringa e la trasforma in maiuscolo, per ottenere 'ASC' o 'DESC'
                // 5. SKIP $skip LIMIT $limit = SKIP salta i primi $skip risultati e LIMIT limita il numero di risultati restituiti a $limit. Questi valori sono passati come parametri alla query
                var cursor = await tx.RunAsync(@$"
                    MATCH (m:Movie)
                    WHERE m.{sort} IS NOT NULL       
                    RETURN m {{
                        .*,
                        favorite: m.tmdbId IN $favorites
                    }} AS movie
                    ORDER BY m.{sort} {order.ToString("G").ToUpper()}
                    SKIP $skip
                    LIMIT $limit", new { skip, limit, favorites });

                // Supponendo di avere questi dati:
                // [
                //   { "movie": { "title": "Inception", "releaseDate": "2010", "director": "Christopher Nolan" } },
                //   { "movie": { "title": "The Matrix", "releaseDate": "1999", "director": "Lana Wachowski, Lilly Wachowski" } }
                // ]
                var records = await cursor.ToListAsync();

                // records diventa:
                // List<IRecord> records = new List<IRecord>
                // {
                //     new Record(new Dictionary<string, object> { { "movie", new Dictionary<string, object> { { "title", "Inception" }, { "releaseDate", "2010" }, { "director", "Christopher Nolan" } } } }),
                //     new Record(new Dictionary<string, object> { { "movie", new Dictionary<string, object> { { "title", "The Matrix" }, { "releaseDate", "1999" }, { "director", "Lana Wachowski, Lilly Wachowski" } } } })
                // };
                var movies = records
                    .Select(x => x["movie"].As<Dictionary<string, object>>())
                    .ToArray();

                // movies diventa:
                //Dictionary<string, object>[] movies = new Dictionary<string, object>[]
                //{
                //    new Dictionary<string, object> { { "title", "Inception" }, { "releaseDate", "2010" }, { "director", "Christopher Nolan" } },
                //    new Dictionary<string, object> { { "title", "The Matrix" }, { "releaseDate", "1999" }, { "director", "Lana Wachowski, Lilly Wachowski" } }
                //};

                return movies;
            });

        }
        // end::all[]

        /// <summary>
        /// Get a paginated list of Movies by Genre. <br/><br/>
        /// Records should be filtered by <see cref="name"/>, ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.<br/><br/>
        /// </summary>
        /// <param name="name">The genre name to filter records by.</param>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getByGenre[]
        public async Task<Dictionary<string, object>[]> GetByGenreAsync(string name, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            // TODO: Get Movies in a Genre
            // MATCH (m:Movie)-[:IN_GENRE]->(:Genre {name: $name})

            return await Task.FromResult(Fixtures.Popular.Skip(skip).Take(limit).ToArray());
        }
        // end::getByGenre[]

        /// <summary>
        /// Get a paginated list of Movies that have ACTED_IN relationship to a Person with <see cref="id"/>.<br/><br/>
        /// Records should be ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">the Person's id.</param>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getForActor[]
        public async Task<Dictionary<string, object>[]> GetForActorAsync(string id, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            // TODO: Get Movies acted in by a Person
            // MATCH (:Person {tmdbId: $id})-[:ACTED_IN]->(m:Movie)

            return await Task.FromResult(Fixtures.Roles.Skip(skip).Take(limit).ToArray());
        }
        // end::getForActor[]

        /// <summary>
        /// Get a paginated list of Movies that have DIRECTED relationship to a Person with <see cref="id"/>.<br/><br/>
        /// Records should be ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">the Person's id.</param>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getForDirector[]
        public async Task<Dictionary<string, object>[]> GetForDirectorAsync(string id, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            // TODO: Get Movies directed by a Person
            // MATCH (:Person {tmdbId: $id})-[:DIRECTED]->(m:Movie)

            return await Task.FromResult(Fixtures.Popular.Skip(skip).Take(limit).ToArray());
        }
        // end::getForDirector[]

        /// <summary>
        /// Find a Movie node with the ID passed as <see cref="id"/>.<br/><br/>
        /// Along with the returned payload, a list of actors, directors, and genres should be included.<br/>
        /// The number of incoming RATED relationships should be returned with key "ratingCount".<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">The tmdbId for a Movie.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a record.
        /// </returns>
        // tag::findById[]
        public async Task<Dictionary<string, object>> FindByIdAsync(string id, string userId = null)
        {
            // TODO: Find a movie by its ID
            // MATCH (m:Movie {tmdbId: $id})

            return await Task.FromResult(Fixtures.Goodfellas);
        }
        // end::findById[]

        /// <summary>
        /// Get a paginated list of similar movies to the Movie with the <see cref="id"/> supplied.<br/>
        /// This similarity is calculated by finding movies that have many first degree connections in common: Actors, Directors and Genres.<br/><br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">The tmdbId for a Movie.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getSimilarMovies[]
        public async Task<Dictionary<string, object>[]> GetSimilarMoviesAsync(string id, int limit, int skip)
        {
            // TODO: Get similar movies based on genres or ratings
            var random = new Random();
            var exampleData = Fixtures.Popular
                .Skip(skip)
                .Take(limit)
                .Select(popularItem =>
                    popularItem.Concat(new[]
                        {
                            new KeyValuePair<string, object>("score", Math.Round(random.NextDouble() * 100, 2))
                        })
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                .ToArray();
            return await Task.FromResult(exampleData);
            // end::getSimilarMovies[]
        }

        /// <summary>
        /// Get a list of tmdbId properties for the movies that the user has added to their "My Favorites" list.
        /// </summary>
        /// <param name="transaction">The open transaction.</param>
        /// <param name="userId">The ID of the current user.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of tmdbIds.
        /// </returns>
        // tag::getUserFavorites[]
        private static async Task<string[]> GetUserFavoritesAsync(IAsyncQueryRunner transaction, string userId)
        {
            if (userId == null)
                return Array.Empty<string>();
            var query = @"
                MATCH (u:User {userId: $userId})-[:HAS_FAVORITE]->(m)
                RETURN m.tmdbId as id";
            var cursor = await transaction.RunAsync(query, new { userId });
            var records = await cursor.ToListAsync();

            return records.Select(x => x["id"].As<string>()).ToArray();
        }
        // end::getUserFavorites[]
    }
}
