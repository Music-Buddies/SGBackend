-- this script is to delete any dummy users and matches with them. Was once needed to cleanup prod
delete
MutualPlaybackEntries, MutualPlaybackOverviews from MutualPlaybackEntries inner join MutualPlaybackOverviews on MutualPlaybackEntries.MutualPlaybackOverviewId = MutualPlaybackOverviews.Id
inner join User U1 on U1.Id = MutualPlaybackOverviews.User1Id inner join User U2 on U2.Id = MutualPlaybackOverviews.User2Id
where U1.Name like 'Dummy%' or U2.Name like 'Dummy%';
    
delete
User from User where User.Name like 'Dummy%'