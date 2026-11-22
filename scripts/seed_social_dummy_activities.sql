-- Dev-only social feed seed: varied activities across active users.
BEGIN;

-- Users
-- 53fc2a7d-a45d-4037-9d88-43c4757253ac madde5-benchmark
-- a900ed4a-3be4-424f-9b7f-53634c2bc4c0 emrepatan91
-- b8ec77d7-637e-4fd0-a836-660d352a4a58 emrepatann

INSERT INTO reviews ("Id", "UserId", "MovieId", "TvShowId", "Content", "AuthoringLocale", "CreatedAt", "UpdatedAt")
VALUES
  ('a1111111-1111-4111-8111-111111111101', '53fc2a7d-a45d-4037-9d88-43c4757253ac', '60beaa75-405b-44d5-ad87-f4e5e0129628', NULL,
   'L.A. Confidential hâlâ neon-soaked noir referansı. Son yarım saat tek başına kurs konusu.', 'tr', NOW() - INTERVAL '2 hours', NOW() - INTERVAL '2 hours'),
  ('a1111111-1111-4111-8111-111111111102', '53fc2a7d-a45d-4037-9d88-43c4757253ac', NULL, 'a836d98e-f6b9-42b7-9379-9802271dd65f',
   'Cold Case binge modunda: dava başına bir playlist, duygusal ama işliyor.', 'tr', NOW() - INTERVAL '5 hours', NOW() - INTERVAL '5 hours'),
  ('a1111111-1111-4111-8111-111111111103', 'a900ed4a-3be4-424f-9b7f-53634c2bc4c0', '5890bd88-7366-4dc2-b6a2-70c590ffed27', NULL,
   'Gece seansı için sessiz ama etkili bir seçim — posterden daha güçlü.', 'tr', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day'),
  ('a1111111-1111-4111-8111-111111111104', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', NULL, '3dfa6a2c-347a-4937-970f-17bfc3289cb0',
   'The Mentalist: case-of-the-week ritmi rahatlatıcı, Red John hattı hâlâ bağımlılık yapıyor.', 'tr', NOW() - INTERVAL '20 hours', NOW() - INTERVAL '20 hours')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO ratings ("Id", "UserId", "MovieId", "TvShowId", "Score", "CreatedAt", "UpdatedAt")
VALUES
  ('b2222222-2222-4222-8222-222222222201', '53fc2a7d-a45d-4037-9d88-43c4757253ac', '86edd672-2a30-4cdf-98fb-e4a81f4ec22d', NULL, 8, NOW() - INTERVAL '3 hours', NOW() - INTERVAL '3 hours'),
  ('b2222222-2222-4222-8222-222222222202', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', 'e5a341b8-5cc8-45ee-b6d2-45b926efa23d', NULL, 6, NOW() - INTERVAL '8 hours', NOW() - INTERVAL '8 hours'),
  ('b2222222-2222-4222-8222-222222222203', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', NULL, 'b963ae9b-4bb9-4f61-b393-823906a01b9d', 9, NOW() - INTERVAL '12 hours', NOW() - INTERVAL '12 hours')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO social_activities (
  "Id", "ActorId", "ActivityType", "SourceEntityId", "TargetContentId", "TargetContentType", "CreatedAt", "IsDeleted")
VALUES
  ('c3333333-3333-4333-8333-333333333301', '53fc2a7d-a45d-4037-9d88-43c4757253ac', 'ReviewedMovie',
    'a1111111-1111-4111-8111-111111111101', '60beaa75-405b-44d5-ad87-f4e5e0129628', 'Movie', NOW() - INTERVAL '2 hours', false),
  ('c3333333-3333-4333-8333-333333333302', '53fc2a7d-a45d-4037-9d88-43c4757253ac', 'RatedMovie',
    'b2222222-2222-4222-8222-222222222201', '86edd672-2a30-4cdf-98fb-e4a81f4ec22d', 'Movie', NOW() - INTERVAL '3 hours', false),
  ('c3333333-3333-4333-8333-333333333303', '53fc2a7d-a45d-4037-9d88-43c4757253ac', 'WatchedMovie',
    'd4444444-4444-4444-8444-444444444401', '0cadda52-6b93-4424-8705-a0f261c16021', 'Movie', NOW() - INTERVAL '6 hours', false),
  ('c3333333-3333-4333-8333-333333333304', '53fc2a7d-a45d-4037-9d88-43c4757253ac', 'FavoritedTvShow',
    'd4444444-4444-4444-8444-444444444402', 'b963ae9b-4bb9-4f61-b393-823906a01b9d', 'TvShow', NOW() - INTERVAL '7 hours', false),
  ('c3333333-3333-4333-8333-333333333305', '53fc2a7d-a45d-4037-9d88-43c4757253ac', 'ReviewedTvShow',
    'a1111111-1111-4111-8111-111111111102', 'a836d98e-f6b9-42b7-9379-9802271dd65f', 'TvShow', NOW() - INTERVAL '5 hours', false),

  ('c3333333-3333-4333-8333-333333333306', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', 'WatchedMovie',
    'd4444444-4444-4444-8444-444444444403', '0b53cf2a-fd5e-4c71-bde1-2eaf69ebaed4', 'Movie', NOW() - INTERVAL '4 hours', false),
  ('c3333333-3333-4333-8333-333333333307', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', 'RatedMovie',
    'b2222222-2222-4222-8222-222222222202', 'e5a341b8-5cc8-45ee-b6d2-45b926efa23d', 'Movie', NOW() - INTERVAL '8 hours', false),
  ('c3333333-3333-4333-8333-333333333308', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', 'FavoritedMovie',
    'd4444444-4444-4444-8444-444444444404', 'bb700c07-2434-4028-9d18-60b2f6727b1f', 'Movie', NOW() - INTERVAL '10 hours', false),
  ('c3333333-3333-4333-8333-333333333309', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', 'WatchedEpisode',
    'd4444444-4444-4444-8444-444444444405', '8fafbc20-b019-4c26-b9ba-888e3974e20b', 'Episode', NOW() - INTERVAL '9 hours', false),
  ('c3333333-3333-4333-8333-333333333310', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', 'RatedTvShow',
    'b2222222-2222-4222-8222-222222222203', 'b963ae9b-4bb9-4f61-b393-823906a01b9d', 'TvShow', NOW() - INTERVAL '12 hours', false),

  ('c3333333-3333-4333-8333-333333333311', 'a900ed4a-3be4-424f-9b7f-53634c2bc4c0', 'ReviewedMovie',
    'a1111111-1111-4111-8111-111111111103', '5890bd88-7366-4dc2-b6a2-70c590ffed27', 'Movie', NOW() - INTERVAL '1 day', false),
  ('c3333333-3333-4333-8333-333333333312', 'a900ed4a-3be4-424f-9b7f-53634c2bc4c0', 'FavoritedTvShow',
    'd4444444-4444-4444-8444-444444444406', 'c3909042-85c5-4937-a1a7-d3d02e9dcf4d', 'TvShow', NOW() - INTERVAL '18 hours', false),
  ('c3333333-3333-4333-8333-333333333313', '53fc2a7d-a45d-4037-9d88-43c4757253ac', 'WatchedMovie',
    'd4444444-4444-4444-8444-444444444407', '86edd672-2a30-4cdf-98fb-e4a81f4ec22d', 'Movie', NOW() - INTERVAL '30 minutes', false),
  ('c3333333-3333-4333-8333-333333333314', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', 'ReviewedTvShow',
    'a1111111-1111-4111-8111-111111111104', '3dfa6a2c-347a-4937-970f-17bfc3289cb0', 'TvShow', NOW() - INTERVAL '20 hours', false)
ON CONFLICT ("Id") DO NOTHING;

-- Comments (highlight feed): review thread + quick takes
INSERT INTO social_comments ("Id", "ActivityId", "AuthorId", "ParentCommentId", "Content", "CreatedAt", "UpdatedAt", "IsDeleted")
VALUES
  ('e5555555-5555-4555-8555-555555555501', 'c3333333-3333-4333-8333-333333333301', 'a900ed4a-3be4-424f-9b7f-53634c2bc4c0', NULL,
   'Bu final twist''i unutamıyorum, soundtrack de taşıyor.', NOW() - INTERVAL '90 minutes', NULL, false),
  ('e5555555-5555-4555-8555-555555555502', 'c3333333-3333-4333-8333-333333333301', '53fc2a7d-a45d-4037-9d88-43c4757253ac', 'e5555555-5555-4555-8555-555555555501',
   'Evet — özellikle interrogation sahnesi masterclass.', NOW() - INTERVAL '70 minutes', NULL, false),
  ('e5555555-5555-4555-8555-555555555503', 'c3333333-3333-4333-8333-333333333306', '53fc2a7d-a45d-4037-9d88-43c4757253ac', NULL,
   '[gif]https://media.giphy.com/media/3o7abKhOpu0NwenH3O/giphy.gif[/gif]', NOW() - INTERVAL '3 hours', NULL, false),
  ('e5555555-5555-4555-8555-555555555504', 'c3333333-3333-4333-8333-333333333313', 'b8ec77d7-637e-4fd0-a836-660d352a4a58', NULL,
   'Bunu da listeye alıyorum, iyi yakalamışsın.', NOW() - INTERVAL '15 minutes', NULL, false)
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

SELECT "ActorId", "ActivityType", count(*) AS n
FROM social_activities
WHERE "IsDeleted" = false
GROUP BY 1, 2
ORDER BY 1, 2;