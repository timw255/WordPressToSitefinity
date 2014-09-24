#WordPressToSitefinity

This is not a standalone project. It must be placed inside of a Sitefinity 7.0 or later project and run from there.

##What it does

This tool imports blog posts from WordPress export files in to Telerik Sitefinity blogs.

**Current Functionality**

* Tags and Categories can be automatically created in Sitefinity and assigned to the posts
* Images found in post content are downloaded from their remote location, stored inside Sitefinity libraries, and included in the new blog post using [sfref]
* Import post comments
* Import post meta data (description, keywords)
* Import post thumbnail images

##Dependencies

* CSQuery

##Please note

Currently, this if intended for developers already familiar with Telerik Sitefinity since it may require some modification depending on the exact scenario.

Suggestions and pull requests are totally welcome!

##How to hook everything up

###What to do first

All authors must already have accounts created in Sitefinity
All custom blog post fields (meta data, related content, etc.) must be created prior to the import

###Copying the source files

Merge the source files into your project.

##Running the import

_Make sure to do a backup of your project (including the database!) before attempting the import. Just in case, ya know. :)_

1. Navigate to ~/Migration/WPM.aspx to upload your WordPress XML dump and select the options you'd like.
2. Click 'Run'
