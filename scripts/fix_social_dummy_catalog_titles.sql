-- Point dummy social activities at catalog movies with real titles + posters.
BEGIN;

-- Known good movies (Title + PosterPath present)
-- 60beaa75-405b-44d5-ad87-f4e5e0129628 L.A. Confidential
-- 86edd672-2a30-4cdf-98fb-e4a81f4ec22d The Hunger Games
-- 0cadda52-6b93-4424-8705-a0f261c16021 Anaconda
-- 0b53cf2a-fd5e-4c71-bde1-2eaf69ebaed4 Scream VI
-- e5a341b8-5cc8-45ee-b6d2-45b926efa23d Scream 4
-- 5890bd88-7366-4dc2-b6a2-70c590ffed27 The Little Mermaid
-- bb700c07-2434-4028-9d18-60b2f6727b1f Lara Croft: Tomb Raider

UPDATE social_activities SET "TargetContentId" = '86edd672-2a30-4cdf-98fb-e4a81f4ec22d'
WHERE "Id" = 'c3333333-3333-4333-8333-333333333302';
UPDATE ratings SET "MovieId" = '86edd672-2a30-4cdf-98fb-e4a81f4ec22d'
WHERE "Id" = 'b2222222-2222-4222-8222-222222222201';

UPDATE social_activities SET "TargetContentId" = '0cadda52-6b93-4424-8705-a0f261c16021'
WHERE "Id" = 'c3333333-3333-4333-8333-333333333303';

UPDATE social_activities SET "TargetContentId" = '0b53cf2a-fd5e-4c71-bde1-2eaf69ebaed4'
WHERE "Id" = 'c3333333-3333-4333-8333-333333333306';

UPDATE social_activities SET "TargetContentId" = 'e5a341b8-5cc8-45ee-b6d2-45b926efa23d'
WHERE "Id" = 'c3333333-3333-4333-8333-333333333307';
UPDATE ratings SET "MovieId" = 'e5a341b8-5cc8-45ee-b6d2-45b926efa23d'
WHERE "Id" = 'b2222222-2222-4222-8222-222222222202';

UPDATE social_activities SET "TargetContentId" = 'bb700c07-2434-4028-9d18-60b2f6727b1f'
WHERE "Id" = 'c3333333-3333-4333-8333-333333333308';

UPDATE social_activities SET "TargetContentId" = '5890bd88-7366-4dc2-b6a2-70c590ffed27'
WHERE "Id" = 'c3333333-3333-4333-8333-333333333311';
UPDATE reviews SET "MovieId" = '5890bd88-7366-4dc2-b6a2-70c590ffed27'
WHERE "Id" = 'a1111111-1111-4111-8111-111111111103';

UPDATE social_activities SET "TargetContentId" = '86edd672-2a30-4cdf-98fb-e4a81f4ec22d'
WHERE "Id" = 'c3333333-3333-4333-8333-333333333313';

COMMIT;

SELECT a."Id", m."Title", left(m."PosterPath", 36) AS poster
FROM social_activities a
JOIN movies m ON m."Id" = a."TargetContentId"
WHERE a."Id"::text LIKE 'c3333333%'
ORDER BY a."CreatedAt" DESC;