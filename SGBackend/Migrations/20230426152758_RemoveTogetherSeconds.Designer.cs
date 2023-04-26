﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SGBackend.Entities;

#nullable disable

namespace SGBackend.Migrations
{
    [DbContext(typeof(SgDbContext))]
    [Migration("20230426152758_RemoveTogetherSeconds")]
    partial class RemoveTogetherSeconds
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("SGBackend.Entities.Artist", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid?>("MediumId")
                        .HasColumnType("char(36)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("MediumId");

                    b.ToTable("Artists");
                });

            modelBuilder.Entity("SGBackend.Entities.Medium", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<string>("AlbumName")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<bool>("ExplicitContent")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("LinkToMedium")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<int>("MediumSource")
                        .HasColumnType("int");

                    b.Property<string>("ReleaseDate")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("LinkToMedium")
                        .IsUnique();

                    b.ToTable("Media");
                });

            modelBuilder.Entity("SGBackend.Entities.MediumImage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid?>("MediumId")
                        .HasColumnType("char(36)");

                    b.Property<int>("height")
                        .HasColumnType("int");

                    b.Property<string>("imageUrl")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("width")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("MediumId");

                    b.ToTable("Images");
                });

            modelBuilder.Entity("SGBackend.Entities.MutualPlaybackEntry", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid>("MediumId")
                        .HasColumnType("char(36)");

                    b.Property<Guid>("MutualPlaybackOverviewId")
                        .HasColumnType("char(36)");

                    b.Property<long>("PlaybackSecondsUser1")
                        .HasColumnType("bigint");

                    b.Property<long>("PlaybackSecondsUser2")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("MutualPlaybackOverviewId");

                    b.HasIndex("MediumId", "MutualPlaybackOverviewId")
                        .IsUnique();

                    b.ToTable("MutualPlaybackEntries");
                });

            modelBuilder.Entity("SGBackend.Entities.MutualPlaybackOverview", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid>("User1Id")
                        .HasColumnType("char(36)");

                    b.Property<Guid>("User2Id")
                        .HasColumnType("char(36)");

                    b.HasKey("Id");

                    b.HasIndex("User2Id");

                    b.HasIndex("User1Id", "User2Id")
                        .IsUnique();

                    b.ToTable("MutualPlaybackOverviews");
                });

            modelBuilder.Entity("SGBackend.Entities.PlaybackRecord", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<Guid>("MediumId")
                        .HasColumnType("char(36)");

                    b.Property<DateTime>("PlayedAt")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("PlayedSeconds")
                        .HasColumnType("int");

                    b.Property<Guid>("UserId")
                        .HasColumnType("char(36)");

                    b.HasKey("Id");

                    b.HasIndex("MediumId");

                    b.HasIndex("UserId", "MediumId", "PlayedAt")
                        .IsUnique();

                    b.ToTable("PlaybackRecords");
                });

            modelBuilder.Entity("SGBackend.Entities.PlaybackSummary", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<DateTime>("LastListened")
                        .HasColumnType("datetime(6)");

                    b.Property<Guid>("MediumId")
                        .HasColumnType("char(36)");

                    b.Property<int>("TotalSeconds")
                        .HasColumnType("int");

                    b.Property<Guid>("UserId")
                        .HasColumnType("char(36)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.HasIndex("MediumId", "UserId")
                        .IsUnique();

                    b.ToTable("PlaybackSummaries");
                });

            modelBuilder.Entity("SGBackend.Entities.State", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<bool>("GroupedFetchJobInstalled")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("QuartzApplied")
                        .HasColumnType("tinyint(1)");

                    b.HasKey("Id");

                    b.ToTable("States");
                });

            modelBuilder.Entity("SGBackend.Entities.Stats", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<DateTime>("LatestFetch")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.ToTable("Stats");
                });

            modelBuilder.Entity("SGBackend.Entities.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<int>("Language")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("SpotifyId")
                        .HasColumnType("longtext");

                    b.Property<string>("SpotifyProfileUrl")
                        .HasColumnType("longtext");

                    b.Property<string>("SpotifyRefreshToken")
                        .HasColumnType("longtext");

                    b.Property<Guid>("StatsId")
                        .HasColumnType("char(36)");

                    b.HasKey("Id");

                    b.HasIndex("StatsId");

                    b.ToTable("User");
                });

            modelBuilder.Entity("SGBackend.Entities.Artist", b =>
                {
                    b.HasOne("SGBackend.Entities.Medium", null)
                        .WithMany("Artists")
                        .HasForeignKey("MediumId");
                });

            modelBuilder.Entity("SGBackend.Entities.MediumImage", b =>
                {
                    b.HasOne("SGBackend.Entities.Medium", null)
                        .WithMany("Images")
                        .HasForeignKey("MediumId");
                });

            modelBuilder.Entity("SGBackend.Entities.MutualPlaybackEntry", b =>
                {
                    b.HasOne("SGBackend.Entities.Medium", "Medium")
                        .WithMany()
                        .HasForeignKey("MediumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("SGBackend.Entities.MutualPlaybackOverview", "MutualPlaybackOverview")
                        .WithMany("MutualPlaybackEntries")
                        .HasForeignKey("MutualPlaybackOverviewId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Medium");

                    b.Navigation("MutualPlaybackOverview");
                });

            modelBuilder.Entity("SGBackend.Entities.MutualPlaybackOverview", b =>
                {
                    b.HasOne("SGBackend.Entities.User", "User1")
                        .WithMany()
                        .HasForeignKey("User1Id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("SGBackend.Entities.User", "User2")
                        .WithMany()
                        .HasForeignKey("User2Id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User1");

                    b.Navigation("User2");
                });

            modelBuilder.Entity("SGBackend.Entities.PlaybackRecord", b =>
                {
                    b.HasOne("SGBackend.Entities.Medium", "Medium")
                        .WithMany()
                        .HasForeignKey("MediumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("SGBackend.Entities.User", "User")
                        .WithMany("PlaybackRecords")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Medium");

                    b.Navigation("User");
                });

            modelBuilder.Entity("SGBackend.Entities.PlaybackSummary", b =>
                {
                    b.HasOne("SGBackend.Entities.Medium", "Medium")
                        .WithMany()
                        .HasForeignKey("MediumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("SGBackend.Entities.User", "User")
                        .WithMany("PlaybackSummaries")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Medium");

                    b.Navigation("User");
                });

            modelBuilder.Entity("SGBackend.Entities.User", b =>
                {
                    b.HasOne("SGBackend.Entities.Stats", "Stats")
                        .WithMany()
                        .HasForeignKey("StatsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Stats");
                });

            modelBuilder.Entity("SGBackend.Entities.Medium", b =>
                {
                    b.Navigation("Artists");

                    b.Navigation("Images");
                });

            modelBuilder.Entity("SGBackend.Entities.MutualPlaybackOverview", b =>
                {
                    b.Navigation("MutualPlaybackEntries");
                });

            modelBuilder.Entity("SGBackend.Entities.User", b =>
                {
                    b.Navigation("PlaybackRecords");

                    b.Navigation("PlaybackSummaries");
                });
#pragma warning restore 612, 618
        }
    }
}
