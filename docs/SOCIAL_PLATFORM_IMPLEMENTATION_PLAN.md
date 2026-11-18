# Social Platform — Implementation Plan

Status: Approved scope / implementation in progress
Branch: `feature/social-platform`
Base: `master`

## Product decisions

- Main feed is mixed: followed users plus discovery-ranked public activities.
- New profiles default to public; users can switch to private.
- Comments use threaded replies (parent-child tree, not a fixed two-level model).
- V1 includes user discovery, follow/unfollow, public profiles, activity feed, likes on activities and comments, threaded comments, social notifications, privacy, blocks, and reports.
- Existing catalog follows (movie/TV release follows) remain separate from user follows.

## Backend architecture

### Domain boundaries

Keep social graph and interactions in dedicated social entities. Do not expose the account profile DTO as a public profile; public responses must be explicit allow-lists and never include email, auth providers, password/security fields, or private account state.

Core entities:
- UserFollow (follower, followed, status, timestamps)
- UserPrivacySettings (profile visibility and activity visibility)
- UserBlock
- SocialActivity (actor, activity type, optional movie/TV/episode/review references, payload snapshot, visibility, timestamps)
- SocialActivityLike (unique activity/user)
- SocialComment (activity, author, nullable parent comment, content, timestamps, soft-delete state)
- SocialCommentLike (unique comment/user)
- SocialReport (reporter, target type/id, reason, status)
- SocialNotification (recipient, actor, type, target references, read state)
- SocialFeedEntry (read-optimized projection if profiling demonstrates it is needed)
- SocialOutboxMessage (transactional delivery record for activity fanout/projection/notification work)

### Activity consistency

User actions that create/update/delete watched records, ratings, favorites, or reviews must write their corresponding social event/outbox record in the same database transaction as the source action. Event processing must be idempotent. Updating a rating/review should update or supersede the matching activity rather than create duplicate activity spam. Removing a source action should retract or tombstone its activity. Do not fabricate events for historical data without a deliberate backfill policy.

### Feed

- Cursor pagination using stable (createdAt, id) ordering; do not use offset pagination for an unbounded social timeline.
- Mixed feed is composed from followed-user activities and a discovery lane of public activities. Ranking must be deterministic and bounded; use recency plus engagement signals with anti-spam caps. Do not allow one actor or one activity to dominate a page.
- Enforce visibility, private-profile follow approval, block/mute exclusions, deleted users, and content removal in the database query/service layer before serialization.
- Group related episode activities at read/presentation time without losing their individual source IDs.

### Threaded comments

- ParentCommentId is nullable and references the same activity.
- Validate parent belongs to the same activity; prevent self-parenting and cycles.
- Support cursor-paged roots and replies, bounded depth/page size in API responses, while storing arbitrary depth. Client can progressively fetch deeper replies.
- Soft delete content while preserving thread structure; return a tombstone when children remain.
- Unique likes and idempotent toggles; server owns counts.

### Security and abuse

- Private profile follow requests require acceptance before follower-only activity is visible.
- Blocking is mutual visibility exclusion for feed/profile/comment/like interactions; blocked users cannot follow or interact.
- Report endpoints are authenticated, rate-limited, and auditable.
- All mutations require authenticated actor identity from ICurrentUser; never trust user IDs supplied by clients.
- Public profile DTOs are explicit projections and omit email and all credential/provider fields.

### API contract (initial target)

- GET /api/social/users?query=&cursor=&pageSize=
- GET /api/social/users/{userId}
- GET /api/social/users/{userId}/followers and /following
- PUT /api/social/users/{userId}/follow (or POST/DELETE pair after API convention review)
- GET/PATCH /api/social/me/privacy
- GET /api/social/feed?cursor=&pageSize=&mode=mixed
- GET /api/social/activities/{activityId}
- POST/DELETE /api/social/activities/{activityId}/like
- GET/POST /api/social/activities/{activityId}/comments
- GET/POST /api/social/comments/{commentId}/replies
- POST/DELETE /api/social/comments/{commentId}/like
- PATCH/DELETE /api/social/comments/{commentId}
- POST /api/social/users/{userId}/block and DELETE counterpart
- POST /api/social/reports
- GET /api/social/notifications; existing notification inbox contracts to be extended only if compatible.

Final route naming must follow existing controller conventions and be documented alongside DTOs.

## Delivery sequence

1. Domain entities, EF configurations, migration, privacy/follow/block invariants.
2. User discovery, public profile, follow requests, followers/following APIs.
3. Transactional social activity capture for existing watch/rating/favorite/review workflows.
4. Feed query and mixed discovery ranking, cursor pagination, privacy/block filtering.
5. Activity/comment likes and threaded comments.
6. Social notifications, reports, moderation and abuse limits.
7. Integration/unit tests, query/index review, API contract docs.

## Quality gates

- Add tests for authorization, private-profile visibility, block exclusion, follow request approval, comment parent validation, cycle prevention, idempotency, cursor stability, and outbox retries.
- Add PostgreSQL integration coverage for constraints and query behavior.
- Keep existing API behavior backward-compatible.
- No Maestro workflows or automated device UI runs; manual device validation is owned by the product owner.
- No direct master commits, merge, or deployment as part of this branch.
